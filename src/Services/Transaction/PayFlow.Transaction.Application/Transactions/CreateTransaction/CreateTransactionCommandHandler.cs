using MediatR;
using PayFlow.SharedKernel;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Domain;
using TransactionAggregate = PayFlow.Transaction.Domain.Transactions.Transaction;

namespace PayFlow.Transaction.Application.Transactions.CreateTransaction;

internal sealed class CreateTransactionCommandHandler
    : IRequestHandler<CreateTransactionCommand, Result<CreateTransactionResponse>>
{
    private readonly ITransactionRepository _transactions;
    private readonly IUnitOfWork _uow;
    private readonly IPaymentClient _payments;
    private readonly IRoutingPolicy _routing;

    public CreateTransactionCommandHandler(
        ITransactionRepository transactions,
        IUnitOfWork uow,
        IPaymentClient payments,
        IRoutingPolicy routing)
    {
        _transactions = transactions;
        _uow = uow;
        _payments = payments;
        _routing = routing;
    }

    public async Task<Result<CreateTransactionResponse>> Handle(
        CreateTransactionCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await _transactions.OrderReferenceExistsAsync(command.TenantId, command.OrderReference, ct))
        {
            return Result.Failure<CreateTransactionResponse>("ORDER_REFERENCE_ALREADY_USED");
        }

        var initResult = TransactionAggregate.Initiate(
            tenantId: command.TenantId,
            orderReference: command.OrderReference,
            amountMinor: command.AmountMinor,
            currency: command.Currency);

        if (initResult.IsFailure)
        {
            return Result.Failure<CreateTransactionResponse>(initResult.ErrorCode!);
        }
        var transaction = initResult.Value;

        // Persist Initiated first — the outbox interceptor writes the
        // transaction.initiated.v1 row in the same SaveChanges.
        await _transactions.AddAsync(transaction, ct);
        await _uow.SaveChangesAsync(ct);

        var providers = _routing.ResolveOrder(command.TenantId);
        var attempts = new List<ProviderAttempt>(providers.Count);
        string? lastAttemptedProvider = null;

        foreach (var providerCode in providers)
        {
            lastAttemptedProvider = providerCode;
            try
            {
                var response = await _payments.ChargeAsync(
                    new PaymentChargeRequest(
                        TenantId: command.TenantId,
                        TransactionId: transaction.Id,
                        ProviderCode: providerCode,
                        AmountMinor: command.AmountMinor,
                        Currency: command.Currency,
                        CardToken: command.CardToken),
                    ct);

                attempts.Add(new ProviderAttempt(
                    ProviderCode: response.ProviderCode,
                    Result: response.Status,
                    DeclineCode: response.DeclineCode,
                    LatencyMilliseconds: response.LatencyMilliseconds));

                switch (response.Status)
                {
                    case "Captured":
                    case "Authorized":
                        // v1: Authorized treated as success; explicit capture lands later.
                        transaction.MarkCaptured(response.ProviderCode, response.ProviderReference!);
                        await _uow.SaveChangesAsync(ct);
                        return BuildResponse(transaction, attempts);

                    case "HardDeclined":
                        transaction.MarkFailed(FailureReason.HardDeclined, providerCodeAttempted: providerCode);
                        await _uow.SaveChangesAsync(ct);
                        return BuildResponse(transaction, attempts);

                    case "SoftDeclined":
                    case "ProviderUnavailable":
                    default:
                        // Try the next provider in the routing list.
                        continue;
                }
            }
            catch (PaymentClientException)
            {
                // Transport-level failure — try the next provider rather than
                // giving up on the merchant; if every provider is unreachable
                // we land on RoutingExhausted below.
                attempts.Add(new ProviderAttempt(
                    ProviderCode: providerCode,
                    Result: "TransportError",
                    DeclineCode: null,
                    LatencyMilliseconds: 0));
                continue;
            }
        }

        transaction.MarkFailed(FailureReason.RoutingExhausted, providerCodeAttempted: lastAttemptedProvider);
        await _uow.SaveChangesAsync(ct);
        return BuildResponse(transaction, attempts);
    }

    private static Result<CreateTransactionResponse> BuildResponse(
        TransactionAggregate t,
        IReadOnlyList<ProviderAttempt> attempts) =>
        Result.Success(new CreateTransactionResponse(
            TransactionId: t.Id,
            Status: t.State.ToString(),
            ProviderCode: t.FinalProviderCode,
            ProviderReference: t.ProviderReference,
            FailureReason: t.FailureReason,
            Attempts: attempts));
}

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

        // Order reference uniqueness per tenant (per docs/database/erd-transaction.md indices).
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

        var providerCode = _routing.PickProvider(command.TenantId);

        // Persist Initiated first, in its own transaction. The Payment call
        // sits *outside* any open DB transaction (per
        // docs/flows/payment-happy-path.md "two DB transactions, not one").
        // Outbox / Initiated event publication land in the next milestone.
        await _transactions.AddAsync(transaction, ct);
        await _uow.SaveChangesAsync(ct);

        PaymentChargeResponse paymentResponse;
        try
        {
            paymentResponse = await _payments.ChargeAsync(
                new PaymentChargeRequest(
                    TenantId: command.TenantId,
                    TransactionId: transaction.Id,
                    ProviderCode: providerCode,
                    AmountMinor: command.AmountMinor,
                    Currency: command.Currency,
                    CardToken: command.CardToken),
                ct);
        }
        catch (PaymentClientException ex)
        {
            transaction.MarkFailed(FailureReason.ProviderError, providerCodeAttempted: providerCode);
            await _uow.SaveChangesAsync(ct);
            return Result.Success(new CreateTransactionResponse(
                TransactionId: transaction.Id,
                Status: transaction.State.ToString(),
                ProviderCode: providerCode,
                ProviderReference: null,
                FailureReason: $"{FailureReason.ProviderError}: {ex.Message}"));
        }

        ApplyPaymentOutcome(transaction, paymentResponse);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new CreateTransactionResponse(
            TransactionId: transaction.Id,
            Status: transaction.State.ToString(),
            ProviderCode: transaction.FinalProviderCode,
            ProviderReference: transaction.ProviderReference,
            FailureReason: transaction.FailureReason));
    }

    private static void ApplyPaymentOutcome(TransactionAggregate transaction, PaymentChargeResponse response)
    {
        switch (response.Status)
        {
            case "Captured":
                transaction.MarkCaptured(response.ProviderCode, response.ProviderReference!);
                break;

            // v1 keeps the headline shape simple — Authorized acts as success
            // for now. Two-step auth/capture lands when its own command does.
            case "Authorized":
                transaction.MarkCaptured(response.ProviderCode, response.ProviderReference!);
                break;

            case "HardDeclined":
                transaction.MarkFailed(FailureReason.HardDeclined, providerCodeAttempted: response.ProviderCode);
                break;

            case "SoftDeclined":
                // Failover would happen here. Until routing rules exist,
                // a soft decline terminates with RoutingExhausted (we tried
                // the single configured provider and it said no).
                transaction.MarkFailed(FailureReason.RoutingExhausted, providerCodeAttempted: response.ProviderCode);
                break;

            case "ProviderUnavailable":
            default:
                transaction.MarkFailed(FailureReason.ProviderError, providerCodeAttempted: response.ProviderCode);
                break;
        }
    }
}

using MediatR;
using PayFlow.SharedKernel;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Domain;
using PayFlow.Transaction.Domain.Refunds;

namespace PayFlow.Transaction.Application.Refunds.RequestRefund;

internal sealed class RequestRefundCommandHandler
    : IRequestHandler<RequestRefundCommand, Result<RequestRefundResponse>>
{
    private readonly ITransactionRepository _transactions;
    private readonly IRefundRepository _refunds;
    private readonly IUnitOfWork _uow;

    public RequestRefundCommandHandler(
        ITransactionRepository transactions,
        IRefundRepository refunds,
        IUnitOfWork uow)
    {
        _transactions = transactions;
        _refunds = refunds;
        _uow = uow;
    }

    public async Task<Result<RequestRefundResponse>> Handle(
        RequestRefundCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var transaction = await _transactions.GetAsync(command.TenantId, command.TransactionId, ct);
        if (transaction is null)
        {
            return Result.Failure<RequestRefundResponse>("TRANSACTION_NOT_FOUND");
        }

        // Only Captured transactions can be refunded. Once a refund completes
        // and Transaction grows a PartiallyRefunded state in M4.E, this check
        // will broaden.
        if (transaction.State != TransactionState.Captured)
        {
            return Result.Failure<RequestRefundResponse>("REFUND_NOT_ALLOWED_IN_STATE");
        }

        // Amount must not push cumulative refunds past the captured amount.
        // We count completed *and* in-flight (Requested/Processing) refunds —
        // failed ones release the reservation, but a not-yet-finished one
        // still holds the captured balance.
        var outstanding = await _refunds.SumOutstandingAmountAsync(
            command.TenantId, command.TransactionId, ct);
        var remaining = transaction.AmountMinor - outstanding;
        if (command.AmountMinor > remaining)
        {
            return Result.Failure<RequestRefundResponse>("REFUND_AMOUNT_EXCEEDS_REMAINING");
        }

        var refundResult = Refund.Request(
            tenantId: command.TenantId,
            transactionId: command.TransactionId,
            amountMinor: command.AmountMinor,
            currency: transaction.Currency,
            requestedBy: command.RequestedBy,
            finalProviderCode: transaction.FinalProviderCode!);

        if (refundResult.IsFailure)
        {
            return Result.Failure<RequestRefundResponse>(refundResult.ErrorCode!);
        }
        var refund = refundResult.Value;

        await _refunds.AddAsync(refund, ct);
        await _uow.SaveChangesAsync(ct);
        // Outbox interceptor wrote `payflow.refund.requested.v1` in the same
        // SaveChanges — the saga starts walking the moment Reconciliation
        // (M4 follow-up) consumes that event.

        return Result.Success(new RequestRefundResponse(
            RefundId: refund.Id,
            TransactionId: refund.TransactionId,
            Status: refund.State.ToString(),
            AmountMinor: refund.AmountMinor,
            Currency: refund.Currency));
    }
}

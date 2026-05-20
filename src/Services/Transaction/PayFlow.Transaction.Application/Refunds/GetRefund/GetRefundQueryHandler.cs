using MediatR;
using PayFlow.SharedKernel;
using PayFlow.Transaction.Application.Abstractions;

namespace PayFlow.Transaction.Application.Refunds.GetRefund;

internal sealed class GetRefundQueryHandler
    : IRequestHandler<GetRefundQuery, Result<GetRefundResponse>>
{
    private readonly IRefundRepository _refunds;

    public GetRefundQueryHandler(IRefundRepository refunds) => _refunds = refunds;

    public async Task<Result<GetRefundResponse>> Handle(GetRefundQuery q, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(q);

        var refund = await _refunds.GetAsync(q.TenantId, q.RefundId, ct);
        if (refund is null || refund.TransactionId != q.TransactionId)
        {
            // Treat tenant/transaction mismatch as "not found" — never leak
            // the existence of a refund that doesn't belong to the caller.
            return Result.Failure<GetRefundResponse>("REFUND_NOT_FOUND");
        }

        return Result.Success(new GetRefundResponse(
            RefundId: refund.Id,
            TransactionId: refund.TransactionId,
            Status: refund.State.ToString(),
            AmountMinor: refund.AmountMinor,
            Currency: refund.Currency,
            RequestedBy: refund.RequestedBy,
            FinalProviderCode: refund.FinalProviderCode,
            ProviderRefundReference: refund.ProviderRefundReference,
            FailureReason: refund.FailureReason,
            RequestedAt: refund.RequestedAt,
            CompletedAt: refund.CompletedAt,
            FailedAt: refund.FailedAt));
    }
}

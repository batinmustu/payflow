using MediatR;
using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Application.Refunds.GetRefund;

public sealed record GetRefundQuery(
    Guid TenantId,
    Guid TransactionId,
    Guid RefundId)
    : IRequest<Result<GetRefundResponse>>;

public sealed record GetRefundResponse(
    Guid RefundId,
    Guid TransactionId,
    string Status,
    long AmountMinor,
    string Currency,
    string RequestedBy,
    string FinalProviderCode,
    string? ProviderRefundReference,
    string? FailureReason,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt);

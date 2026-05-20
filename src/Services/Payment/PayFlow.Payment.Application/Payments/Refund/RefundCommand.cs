using MediatR;
using PayFlow.Payment.Domain;
using PayFlow.SharedKernel;

namespace PayFlow.Payment.Application.Payments.Refund;

public sealed record RefundCommand(
    Guid TenantId,
    Guid TransactionId,
    string ProviderCode,
    string ProviderReference,
    long AmountMinor,
    string Currency,
    string IdempotencyKey)
    : IRequest<Result<RefundResponse>>;

public sealed record RefundResponse(
    string ProviderCode,
    RefundStatus Status,
    string? ProviderReference,
    string? DeclineCode,
    long LatencyMilliseconds);

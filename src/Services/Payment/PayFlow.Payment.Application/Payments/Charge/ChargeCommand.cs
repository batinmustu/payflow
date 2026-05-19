using MediatR;
using PayFlow.Payment.Domain;
using PayFlow.SharedKernel;

namespace PayFlow.Payment.Application.Payments.Charge;

public sealed record ChargeCommand(
    Guid TenantId,
    Guid TransactionId,
    string ProviderCode,
    long AmountMinor,
    string Currency,
    string CardToken)
    : IRequest<Result<ChargeResponse>>;

public sealed record ChargeResponse(
    Guid PaymentId,
    string ProviderCode,
    PaymentStatus Status,
    string? ProviderReference,
    string? DeclineCode,
    long LatencyMilliseconds);

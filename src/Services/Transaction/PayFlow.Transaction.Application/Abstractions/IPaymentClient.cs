namespace PayFlow.Transaction.Application.Abstractions;

/// <summary>
/// The thin synchronous wire to the Payment service. Defined in Application
/// (an interface Transaction depends on) and implemented in Infrastructure
/// (the HTTP client that actually calls Payment over the network). Kept
/// minimal — Transaction does not need to know that Payment internally
/// dispatches to one of three adapters.
/// </summary>
public interface IPaymentClient
{
    Task<PaymentChargeResponse> ChargeAsync(PaymentChargeRequest request, CancellationToken ct);
}

/// <summary>Payload Transaction sends to Payment.</summary>
public sealed record PaymentChargeRequest(
    Guid TenantId,
    Guid TransactionId,
    string ProviderCode,
    long AmountMinor,
    string Currency,
    string CardToken);

/// <summary>What Payment returns. Mirrors the public response shape.</summary>
public sealed record PaymentChargeResponse(
    Guid PaymentId,
    string ProviderCode,
    string Status,                // "Captured" / "Authorized" / "SoftDeclined" / ...
    string? ProviderReference,
    string? DeclineCode,
    long LatencyMilliseconds);

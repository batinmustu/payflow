namespace PayFlow.Reconciliation.Application.Abstractions;

/// <summary>
/// Application-layer port that abstracts the Payment service's refund
/// endpoint. Infrastructure provides the concrete <c>HttpPaymentRefundClient</c>;
/// tests substitute a fake that returns canned outcomes per provider/amount.
/// </summary>
public interface IPaymentRefundClient
{
    Task<PaymentRefundResponse> RefundAsync(PaymentRefundRequest request, CancellationToken ct);
}

public sealed record PaymentRefundRequest(
    Guid TenantId,
    Guid TransactionId,
    string ProviderCode,
    string ProviderReference,
    long AmountMinor,
    string Currency,
    string IdempotencyKey);

public sealed record PaymentRefundResponse(
    string ProviderCode,
    string Status, // "Refunded" | "Declined" | "ProviderUnavailable"
    string? ProviderReference,
    string? DeclineCode,
    long LatencyMilliseconds);

/// <summary>Thrown by the HTTP client for non-2xx responses or transport failures.</summary>
public sealed class PaymentClientException : Exception
{
    public PaymentClientException(string message) : base(message) { }
    public PaymentClientException(string message, Exception inner) : base(message, inner) { }
}

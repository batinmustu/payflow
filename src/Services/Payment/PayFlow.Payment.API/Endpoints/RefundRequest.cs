namespace PayFlow.Payment.API.Endpoints;

/// <summary>
/// Tenant scope comes from the JWT's <c>tid</c> claim, never from the body.
/// </summary>
public sealed record RefundRequest(
    Guid TransactionId,
    string ProviderCode,
    string ProviderReference,
    long AmountMinor,
    string Currency,
    string IdempotencyKey);

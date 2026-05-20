namespace PayFlow.Payment.API.Endpoints;

public sealed record RefundRequest(
    Guid TenantId,
    Guid TransactionId,
    string ProviderCode,
    string ProviderReference,
    long AmountMinor,
    string Currency,
    string IdempotencyKey);

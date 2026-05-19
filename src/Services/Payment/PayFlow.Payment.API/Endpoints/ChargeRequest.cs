namespace PayFlow.Payment.API.Endpoints;

public sealed record ChargeRequest(
    Guid TenantId,
    Guid TransactionId,
    string ProviderCode,
    long AmountMinor,
    string Currency,
    string CardToken);

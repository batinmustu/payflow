namespace PayFlow.Payment.API.Endpoints;

/// <summary>
/// Tenant scope comes from the JWT's <c>tid</c> claim, never from the body —
/// otherwise a caller with one tenant's token could charge another tenant's
/// merchant.
/// </summary>
public sealed record ChargeRequest(
    Guid TransactionId,
    string ProviderCode,
    long AmountMinor,
    string Currency,
    string CardToken);

namespace PayFlow.Transaction.API.Endpoints;

/// <summary>
/// HTTP request body for <c>POST /api/transactions</c>. The tenant id is
/// not part of the body — it is taken from the authenticated principal's
/// <c>tid</c> claim by <see cref="PayFlow.Multitenancy.TenantContextMiddleware"/>.
/// </summary>
public sealed record CreateTransactionRequest(
    string OrderReference,
    long AmountMinor,
    string Currency,
    string CardToken);

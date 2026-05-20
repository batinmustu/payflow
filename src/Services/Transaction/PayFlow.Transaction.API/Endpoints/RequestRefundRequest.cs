namespace PayFlow.Transaction.API.Endpoints;

/// <summary>
/// Body for <c>POST /api/transactions/{id}/refunds</c>. The transaction id
/// comes from the route, the tenant id from the JWT — only the amount is in
/// the body. <c>RequestedBy</c> is optional; if omitted the endpoint uses
/// the JWT's <c>sub</c> claim.
/// </summary>
public sealed record RequestRefundRequest(
    long AmountMinor,
    string? RequestedBy);

namespace PayFlow.Transaction.API.Endpoints;

/// <summary>
/// Body for <c>POST /api/transactions/{id}/refunds</c>. The transaction id
/// comes from the route, the tenant id from the JWT — only the amount is
/// in the body. <c>RequestedBy</c> is always sourced from the JWT's
/// <c>sub</c> claim server-side; the body has no audit field so a caller
/// can't smuggle a free-form string (e.g. an email) into the outbox /
/// Kafka payload.
/// </summary>
public sealed record RequestRefundRequest(long AmountMinor);

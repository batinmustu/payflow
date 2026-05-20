namespace PayFlow.Reconciliation.Application.RefundSagas;

/// <summary>
/// The shape Reconciliation expects to read from
/// <c>payflow.refund.requested.v1</c>. Mirrors what TX's
/// <c>RefundRequestedDomainEvent</c> serialises into — kept duplicated by
/// bounded-context convention (each service owns its read model).
/// </summary>
public sealed record RefundRequestedIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid TenantId { get; init; }
    public Guid RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public long AmountMinor { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public string FinalProviderCode { get; init; } = string.Empty;
    public string FinalProviderReference { get; init; } = string.Empty;
}

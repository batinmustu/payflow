namespace PayFlow.Reporting.Application.Projections;

/// <summary>
/// <c>payflow.transaction.initiated.v1</c>. Reporting buckets it as "attempted".
/// </summary>
public sealed record TransactionInitiatedIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid TenantId { get; init; }
    public Guid TransactionId { get; init; }
    public string OrderReference { get; init; } = string.Empty;
    public long AmountMinor { get; init; }
    public string Currency { get; init; } = string.Empty;
}

/// <summary>
/// <c>payflow.transaction.captured.v1</c>. Reporting buckets it as "captured"
/// and adds to the per-currency volume.
/// </summary>
public sealed record TransactionCapturedIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid TenantId { get; init; }
    public Guid TransactionId { get; init; }
    public string ProviderCode { get; init; } = string.Empty;
    public string ProviderReference { get; init; } = string.Empty;
    public long AmountMinor { get; init; }
    public string Currency { get; init; } = string.Empty;
}

/// <summary>
/// <c>payflow.transaction.failed.v1</c>. Reporting buckets it as "failed".
/// </summary>
public sealed record TransactionFailedIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid TenantId { get; init; }
    public Guid TransactionId { get; init; }
    public string FailureReason { get; init; } = string.Empty;
    public string? ProviderCodeAttempted { get; init; }
    public string Currency { get; init; } = string.Empty;
}

/// <summary>
/// <c>payflow.refund.completed.v1</c> as Reconciliation publishes it.
/// Reporting buckets it as "refunded" + adds to per-currency refund volume.
/// </summary>
public sealed record RefundCompletedIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid TenantId { get; init; }
    public Guid SagaId { get; init; }
    public Guid RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public long AmountMinor { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string ProviderCode { get; init; } = string.Empty;
    public string ProviderReference { get; init; } = string.Empty;
}

namespace PayFlow.Transaction.Application.Refunds.SagaConsumers;

/// <summary>
/// Shape of <c>payflow.refund.processing.v1</c> as Reconciliation publishes
/// it. TX consumes this to flip its local <c>refunds</c> row from
/// <c>Requested</c> → <c>Processing</c>.
/// </summary>
public sealed record RefundProcessingIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid SagaId { get; init; }
    public Guid RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid TenantId { get; init; }
}

/// <summary>
/// Shape of <c>payflow.refund.completed.v1</c>. TX flips the refund row to
/// <c>Completed</c> and walks the parent transaction's state machine
/// (Captured/PartiallyRefunded → PartiallyRefunded/Refunded based on the
/// new <c>refunded_amount_minor</c>).
/// </summary>
public sealed record RefundCompletedIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid SagaId { get; init; }
    public Guid RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid TenantId { get; init; }
    public long AmountMinor { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string ProviderCode { get; init; } = string.Empty;
    public string ProviderReference { get; init; } = string.Empty;
}

/// <summary>
/// Shape of <c>payflow.refund.failed.v1</c>. TX flips the refund row to
/// <c>Failed</c>; the parent transaction stays in its current state (the
/// captured amount is still available for another refund attempt).
/// </summary>
public sealed record RefundFailedIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid SagaId { get; init; }
    public Guid RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid TenantId { get; init; }
    public string FailureReason { get; init; } = string.Empty;
}

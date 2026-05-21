namespace PayFlow.Webhooks.Application.Deliveries;

// Webhook's read-models of the producer-side integration events. Same
// schema as the Notification service consumes — kept separate so each
// service owns its own view of the contract.

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

public sealed record RefundFailedIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid TenantId { get; init; }
    public Guid SagaId { get; init; }
    public Guid RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public string FailureReason { get; init; } = string.Empty;
}

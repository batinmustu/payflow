namespace PayFlow.Notification.Application.Notifications;

/// <summary>
/// Notification's read-model of <c>payflow.transaction.captured.v1</c>.
/// Producer-side fields kept here are the ones the merchant template needs.
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

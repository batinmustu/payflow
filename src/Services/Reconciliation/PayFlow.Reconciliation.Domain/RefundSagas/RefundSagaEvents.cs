using PayFlow.SharedKernel;

namespace PayFlow.Reconciliation.Domain.RefundSagas;

/// <summary>
/// Emitted when the saga starts (Reconciliation accepted the refund request
/// and is about to call the provider). Tells Transaction to flip its refund
/// row from Requested → Processing.
/// </summary>
public sealed record RefundProcessingDomainEvent(
    Guid SagaId,
    Guid RefundId,
    Guid TransactionId,
    Guid TenantId)
    : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => SagaId;
    string IIntegrationDomainEvent.EventType => "payflow.refund.processing.v1";
}

/// <summary>
/// Emitted when the provider returned <c>Refunded</c>. Tells Transaction to
/// flip the refund row → Completed and update the parent transaction's
/// state machine; Notification consumes the same event for the merchant
/// confirmation email.
/// </summary>
public sealed record RefundCompletedDomainEvent(
    Guid SagaId,
    Guid RefundId,
    Guid TransactionId,
    Guid TenantId,
    long AmountMinor,
    string Currency,
    string ProviderCode,
    string ProviderReference)
    : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => SagaId;
    string IIntegrationDomainEvent.EventType => "payflow.refund.completed.v1";
}

/// <summary>
/// Emitted when the provider declined the refund or the saga exhausted its
/// retry budget. Transaction flips the refund row → Failed but leaves the
/// parent transaction in Captured (the captured amount stays available for
/// another refund attempt).
/// </summary>
public sealed record RefundFailedDomainEvent(
    Guid SagaId,
    Guid RefundId,
    Guid TransactionId,
    Guid TenantId,
    string FailureReason)
    : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => SagaId;
    string IIntegrationDomainEvent.EventType => "payflow.refund.failed.v1";
}

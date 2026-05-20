using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Domain.Refunds;

public sealed record RefundRequestedDomainEvent(
    Guid RefundId,
    Guid TransactionId,
    Guid TenantId,
    long AmountMinor,
    string Currency,
    string RequestedBy,
    string FinalProviderCode)
    : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => RefundId;
    string IIntegrationDomainEvent.EventType => "payflow.refund.requested.v1";
}

public sealed record RefundCompletedDomainEvent(
    Guid RefundId,
    Guid TransactionId,
    Guid TenantId,
    long AmountMinor,
    string Currency,
    string ProviderCode,
    string ProviderReference)
    : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => RefundId;
    string IIntegrationDomainEvent.EventType => "payflow.refund.completed.v1";
}

public sealed record RefundFailedDomainEvent(
    Guid RefundId,
    Guid TransactionId,
    Guid TenantId,
    string FailureReason)
    : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => RefundId;
    string IIntegrationDomainEvent.EventType => "payflow.refund.failed.v1";
}

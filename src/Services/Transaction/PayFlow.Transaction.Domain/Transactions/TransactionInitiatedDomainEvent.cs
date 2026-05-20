using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Domain.Transactions;

public sealed record TransactionInitiatedDomainEvent(
    Guid TransactionId,
    Guid TenantId,
    string OrderReference,
    long AmountMinor,
    string Currency) : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => TransactionId;
    string IIntegrationDomainEvent.EventType => "payflow.transaction.initiated.v1";
}

public sealed record TransactionCapturedDomainEvent(
    Guid TransactionId,
    Guid TenantId,
    string ProviderCode,
    string ProviderReference,
    long AmountMinor,
    string Currency) : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => TransactionId;
    string IIntegrationDomainEvent.EventType => "payflow.transaction.captured.v1";
}

public sealed record TransactionFailedDomainEvent(
    Guid TransactionId,
    Guid TenantId,
    string FailureReason,
    string? ProviderCodeAttempted,
    string Currency) : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => TransactionId;
    string IIntegrationDomainEvent.EventType => "payflow.transaction.failed.v1";
}

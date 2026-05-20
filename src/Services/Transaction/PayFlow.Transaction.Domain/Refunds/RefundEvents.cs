using PayFlow.SharedKernel;

namespace PayFlow.Transaction.Domain.Refunds;

/// <summary>
/// The only refund integration event TX emits. The <c>Processing</c>,
/// <c>Completed</c>, and <c>Failed</c> events all flow the other way — from
/// the Reconciliation saga back into TX — so we don't model them here.
/// </summary>
public sealed record RefundRequestedDomainEvent(
    Guid RefundId,
    Guid TransactionId,
    Guid TenantId,
    long AmountMinor,
    string Currency,
    string RequestedBy,
    string FinalProviderCode,
    string FinalProviderReference)
    : DomainEvent, IIntegrationDomainEvent
{
    Guid IIntegrationDomainEvent.AggregateId => RefundId;
    string IIntegrationDomainEvent.EventType => "payflow.refund.requested.v1";
}

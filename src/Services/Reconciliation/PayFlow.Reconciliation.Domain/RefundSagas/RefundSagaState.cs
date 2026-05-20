namespace PayFlow.Reconciliation.Domain.RefundSagas;

/// <summary>
/// Lifecycle of a refund-saga row. Started when Reconciliation consumes
/// <c>payflow.refund.requested.v1</c>; advances through ProviderCalled →
/// Completed/Failed as the provider call resolves. <c>Compensating</c> is
/// reserved for the future case where a downstream consumer's failure
/// requires us to cancel a refund we already authorised (see
/// docs/flows/refund-saga.md).
/// </summary>
public enum RefundSagaState
{
    Started,
    ProviderCalled,
    Completed,
    Failed,
    Compensating,
}

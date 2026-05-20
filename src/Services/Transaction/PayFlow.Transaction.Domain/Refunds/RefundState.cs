namespace PayFlow.Transaction.Domain.Refunds;

/// <summary>
/// Lifecycle of a refund record. Set by Transaction on creation
/// (<see cref="Requested"/>) and updated as the choreography in
/// docs/flows/refund-saga.md walks through Processing → Completed/Failed.
/// </summary>
public enum RefundState
{
    Requested,
    Processing,
    Completed,
    Failed,
}

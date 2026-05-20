namespace PayFlow.Transaction.Domain;

/// <summary>
/// Lifecycle states for a Transaction aggregate. v1 covers the headline
/// happy path + terminal failure; the fuller machine
/// (Authorized → Captured, PartiallyRefunded, Voided, Refunded) lands when
/// the corresponding flows (capture, refund, void) get their commands.
/// See docs/state-machines/transaction.md for the target shape.
/// </summary>
public enum TransactionState
{
    Initiated,
    Captured,
    PartiallyRefunded,
    Refunded,
    Failed,
}

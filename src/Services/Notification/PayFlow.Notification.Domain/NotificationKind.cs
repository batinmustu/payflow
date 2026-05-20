namespace PayFlow.Notification.Domain;

/// <summary>
/// What template + recipients to use. Maps 1:1 to a Scriban file in
/// <c>Templates/</c> and to the producing integration event:
/// <list type="bullet">
///   <item><c>TransactionCaptured</c> ← payflow.transaction.captured.v1</item>
///   <item><c>RefundCompleted</c>    ← payflow.refund.completed.v1</item>
///   <item><c>RefundFailed</c>       ← payflow.refund.failed.v1</item>
/// </list>
/// </summary>
public enum NotificationKind
{
    TransactionCaptured,
    RefundCompleted,
    RefundFailed,
}

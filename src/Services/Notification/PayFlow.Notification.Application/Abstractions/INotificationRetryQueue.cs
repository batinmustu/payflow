using System.Diagnostics.CodeAnalysis;

namespace PayFlow.Notification.Application.Abstractions;

/// <summary>
/// Schedules a retry attempt for a notification whose previous send failed
/// transiently. The infrastructure adapter decides where the message goes
/// (RabbitMQ with TTL + DLX in production, no-op in tests / local dev with
/// no broker) and how long to wait — the dispatcher just hands off the id +
/// the upcoming attempt number.
/// </summary>
[SuppressMessage(
    "Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "This abstraction is a queue facade; the suffix matches caller intent.")]
public interface INotificationRetryQueue
{
    /// <param name="notificationId">Audit row to redispatch.</param>
    /// <param name="nextAttempt">
    /// The attempt this message represents (1 = first retry after the initial
    /// failure). Used to compute the backoff delay.
    /// </param>
    Task EnqueueAsync(Guid notificationId, int nextAttempt, CancellationToken ct);

    /// <summary>
    /// Park a notification whose retry budget is exhausted. The audit row is
    /// already in <c>Failed</c>; this surface exists so the broker keeps a
    /// poison-message record on the DLQ for ops to inspect.
    /// </summary>
    Task ParkOnDlqAsync(Guid notificationId, string reason, CancellationToken ct);
}

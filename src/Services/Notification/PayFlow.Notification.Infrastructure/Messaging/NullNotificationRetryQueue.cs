using Microsoft.Extensions.Logging;
using PayFlow.Notification.Application.Abstractions;

namespace PayFlow.Notification.Infrastructure.Messaging;

/// <summary>
/// Fallback <see cref="INotificationRetryQueue"/> used when RabbitMQ is not
/// configured (typical in unit tests, or a developer who hasn't started the
/// rabbitmq container). Logs the would-be enqueue + dlq operations but
/// performs no actual work — the notification stays Pending after a
/// transient failure and there is no retry consumer to redeliver it.
/// </summary>
internal sealed class NullNotificationRetryQueue : INotificationRetryQueue
{
    private readonly ILogger<NullNotificationRetryQueue> _logger;
    public NullNotificationRetryQueue(ILogger<NullNotificationRetryQueue> logger) => _logger = logger;

    public Task EnqueueAsync(Guid notificationId, int nextAttempt, CancellationToken ct)
    {
        _logger.LogWarning(
            "RabbitMQ not configured — would have scheduled retry attempt {Attempt} for {NotificationId}. " +
            "The notification stays Pending and will not be retried.",
            nextAttempt, notificationId);
        return Task.CompletedTask;
    }

    public Task ParkOnDlqAsync(Guid notificationId, string reason, CancellationToken ct)
    {
        _logger.LogWarning(
            "RabbitMQ not configured — would have parked {NotificationId} on DLQ ({Reason}).",
            notificationId, reason);
        return Task.CompletedTask;
    }
}

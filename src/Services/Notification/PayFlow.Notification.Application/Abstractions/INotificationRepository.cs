using PayFlow.Notification.Domain.Notifications;

namespace PayFlow.Notification.Application.Abstractions;

public interface INotificationRepository
{
    Task<bool> ExistsForSourceMessageAsync(Guid sourceMessageId, CancellationToken ct);

    Task<IReadOnlyList<NotificationRecord>> ListAsync(
        Guid tenantId, int take, CancellationToken ct);

    Task<NotificationRecord?> GetAsync(Guid notificationId, CancellationToken ct);

    Task AddAsync(NotificationRecord record, CancellationToken ct);
}

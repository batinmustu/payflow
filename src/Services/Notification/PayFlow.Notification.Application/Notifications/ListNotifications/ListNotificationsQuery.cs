using MediatR;
using PayFlow.SharedKernel;

namespace PayFlow.Notification.Application.Notifications.ListNotifications;

public sealed record ListNotificationsQuery(Guid TenantId, int Take)
    : IRequest<Result<ListNotificationsResponse>>;

public sealed record ListNotificationsResponse(IReadOnlyList<NotificationListItem> Items);

public sealed record NotificationListItem(
    Guid NotificationId,
    string Kind,
    string Channel,
    string Recipient,
    string Subject,
    string Status,
    string? FailureReason,
    string? ProviderReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? FailedAt);

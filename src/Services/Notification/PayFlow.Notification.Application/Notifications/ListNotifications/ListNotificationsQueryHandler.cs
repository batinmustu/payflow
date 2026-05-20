using MediatR;
using PayFlow.Notification.Application.Abstractions;
using PayFlow.SharedKernel;

namespace PayFlow.Notification.Application.Notifications.ListNotifications;

internal sealed class ListNotificationsQueryHandler
    : IRequestHandler<ListNotificationsQuery, Result<ListNotificationsResponse>>
{
    private const int MaxTake = 200;
    private readonly INotificationRepository _repo;

    public ListNotificationsQueryHandler(INotificationRepository repo) => _repo = repo;

    public async Task<Result<ListNotificationsResponse>> Handle(
        ListNotificationsQuery q, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(q);
        if (q.TenantId == Guid.Empty)
        {
            return Result.Failure<ListNotificationsResponse>("TENANT_REQUIRED");
        }
        var take = Math.Clamp(q.Take <= 0 ? 50 : q.Take, 1, MaxTake);

        var rows = await _repo.ListAsync(q.TenantId, take, ct);
        var items = rows.Select(r => new NotificationListItem(
            NotificationId: r.Id,
            Kind: r.Kind.ToString(),
            Channel: r.Channel.ToString(),
            Recipient: r.Recipient,
            Subject: r.Subject,
            Status: r.State.ToString(),
            FailureReason: r.FailureReason,
            ProviderReference: r.ProviderReference,
            CreatedAt: r.CreatedAt,
            SentAt: r.SentAt,
            FailedAt: r.FailedAt)).ToList();

        return Result.Success(new ListNotificationsResponse(items));
    }
}

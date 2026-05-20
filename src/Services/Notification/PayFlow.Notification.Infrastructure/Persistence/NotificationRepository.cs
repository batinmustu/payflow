using Microsoft.EntityFrameworkCore;
using PayFlow.Notification.Application.Abstractions;
using PayFlow.Notification.Domain.Notifications;

namespace PayFlow.Notification.Infrastructure.Persistence;

internal sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;
    public NotificationRepository(NotificationDbContext db) => _db = db;

    public Task<bool> ExistsForSourceMessageAsync(Guid sourceMessageId, CancellationToken ct) =>
        _db.Set<NotificationRecord>().AnyAsync(n => n.SourceMessageId == sourceMessageId, ct);

    public async Task<IReadOnlyList<NotificationRecord>> ListAsync(
        Guid tenantId, int take, CancellationToken ct) =>
        await _db.Set<NotificationRecord>()
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(NotificationRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _db.Set<NotificationRecord>().AddAsync(record, ct);
    }
}

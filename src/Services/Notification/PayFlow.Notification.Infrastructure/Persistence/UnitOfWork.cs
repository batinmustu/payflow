using PayFlow.Notification.Application.Abstractions;
using PayFlow.Outbox;

namespace PayFlow.Notification.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly NotificationDbContext _db;
    public UnitOfWork(NotificationDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        PostgresExceptionTranslator.RunAndTranslateAsync(_db.SaveChangesAsync, ct);
}

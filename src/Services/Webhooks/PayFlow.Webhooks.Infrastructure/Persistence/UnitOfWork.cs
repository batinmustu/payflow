using PayFlow.Webhooks.Application.Abstractions;

namespace PayFlow.Webhooks.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly WebhooksDbContext _db;
    public UnitOfWork(WebhooksDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

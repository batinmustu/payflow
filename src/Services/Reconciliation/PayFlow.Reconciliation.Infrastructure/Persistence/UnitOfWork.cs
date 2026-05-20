using PayFlow.Reconciliation.Application.Abstractions;

namespace PayFlow.Reconciliation.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly ReconciliationDbContext _db;
    public UnitOfWork(ReconciliationDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

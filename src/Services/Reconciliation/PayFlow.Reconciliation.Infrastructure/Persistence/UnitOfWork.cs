using PayFlow.Outbox;
using PayFlow.Reconciliation.Application.Abstractions;

namespace PayFlow.Reconciliation.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly ReconciliationDbContext _db;
    public UnitOfWork(ReconciliationDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        PostgresExceptionTranslator.RunAndTranslateAsync(_db.SaveChangesAsync, ct);
}

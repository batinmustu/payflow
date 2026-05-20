using PayFlow.Identity.Application.Abstractions;
using PayFlow.Outbox;

namespace PayFlow.Identity.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly IdentityDbContext _db;

    public UnitOfWork(IdentityDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        PostgresExceptionTranslator.RunAndTranslateAsync(_db.SaveChangesAsync, ct);
}

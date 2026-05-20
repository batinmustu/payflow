using PayFlow.Outbox;
using PayFlow.Reporting.Application.Abstractions;

namespace PayFlow.Reporting.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly ReportingDbContext _db;
    public UnitOfWork(ReportingDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct) =>
        PostgresExceptionTranslator.RunAndTranslateAsync(_db.SaveChangesAsync, ct);
}

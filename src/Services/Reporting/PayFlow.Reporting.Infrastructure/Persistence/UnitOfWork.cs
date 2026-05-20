using Microsoft.EntityFrameworkCore;
using Npgsql;
using PayFlow.Reporting.Application.Abstractions;
using PayFlow.SharedKernel;

namespace PayFlow.Reporting.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly ReportingDbContext _db;
    public UnitOfWork(ReportingDbContext db) => _db = db;

    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }
}

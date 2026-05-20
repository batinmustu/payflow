using Microsoft.EntityFrameworkCore;
using Npgsql;
using PayFlow.Identity.Application.Abstractions;
using PayFlow.SharedKernel;

namespace PayFlow.Identity.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly IdentityDbContext _db;

    public UnitOfWork(IdentityDbContext db) => _db = db;

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

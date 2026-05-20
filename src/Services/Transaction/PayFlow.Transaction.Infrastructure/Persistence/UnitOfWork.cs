using Microsoft.EntityFrameworkCore;
using Npgsql;
using PayFlow.SharedKernel;
using PayFlow.Transaction.Application.Abstractions;

namespace PayFlow.Transaction.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly TransactionDbContext _db;

    public UnitOfWork(TransactionDbContext db) => _db = db;

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

    public async Task<T> ExecuteSerialisedAsync<T>(
        Guid lockKey,
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);

        // pg_advisory_xact_lock takes two int4 keys (combined into an int8
        // lock id). Splitting the Guid into two halves spreads collisions
        // across the lock space — only the rare hash collision can lock
        // unrelated rows together, which is harmless (just brief serial
        // execution).
        var (k1, k2) = SplitToInt32(lockKey);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({k1}, {k2})", ct);

        var result = await work(ct);

        await tx.CommitAsync(ct);
        return result;
    }

    private static (int k1, int k2) SplitToInt32(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!id.TryWriteBytes(bytes))
        {
            throw new InvalidOperationException("Failed to serialise Guid for advisory lock.");
        }
        var k1 = BitConverter.ToInt32(bytes[..4]) ^ BitConverter.ToInt32(bytes[4..8]);
        var k2 = BitConverter.ToInt32(bytes[8..12]) ^ BitConverter.ToInt32(bytes[12..16]);
        return (k1, k2);
    }
}

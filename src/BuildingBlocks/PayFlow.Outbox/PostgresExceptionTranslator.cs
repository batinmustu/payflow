using Microsoft.EntityFrameworkCore;
using Npgsql;
using PayFlow.SharedKernel;

namespace PayFlow.Outbox;

/// <summary>
/// Maps Postgres-specific error codes that bubble up through EF Core into
/// the transport-neutral exceptions defined in
/// <see cref="PayFlow.SharedKernel"/>. Application-layer code catches the
/// neutral exception without ever taking a Npgsql/EF dependency.
/// </summary>
public static class PostgresExceptionTranslator
{
    public static async Task<int> RunAndTranslateAsync(
        Func<CancellationToken, Task<int>> work,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        try
        {
            return await work(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }
}

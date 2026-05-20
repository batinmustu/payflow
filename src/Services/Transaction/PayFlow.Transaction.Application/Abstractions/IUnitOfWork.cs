namespace PayFlow.Transaction.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Run <paramref name="work"/> inside a DB transaction that holds a
    /// per-resource advisory lock for the lifetime of the transaction.
    /// Used to serialise refund-reservation reads + writes for the same
    /// transaction id: a concurrent caller blocks on the lock instead of
    /// racing the <c>SELECT SUM</c> + insert.
    /// </summary>
    Task<T> ExecuteSerialisedAsync<T>(
        Guid lockKey,
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct);
}

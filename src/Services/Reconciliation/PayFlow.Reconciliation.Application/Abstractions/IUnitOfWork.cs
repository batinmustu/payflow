namespace PayFlow.Reconciliation.Application.Abstractions;

/// <summary>
/// Mirrors the abstraction used by the other services — keeps Application
/// layer ignorant of EF Core. Concrete implementation wraps DbContext.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}

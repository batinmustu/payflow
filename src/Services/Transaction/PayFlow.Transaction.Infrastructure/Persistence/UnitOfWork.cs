using PayFlow.Transaction.Application.Abstractions;

namespace PayFlow.Transaction.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly TransactionDbContext _db;
    public UnitOfWork(TransactionDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}

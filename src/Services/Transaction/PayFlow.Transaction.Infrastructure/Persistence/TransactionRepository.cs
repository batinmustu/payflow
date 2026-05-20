using Microsoft.EntityFrameworkCore;
using PayFlow.Transaction.Application.Abstractions;
using TransactionAggregate = PayFlow.Transaction.Domain.Transactions.Transaction;

namespace PayFlow.Transaction.Infrastructure.Persistence;

internal sealed class TransactionRepository : ITransactionRepository
{
    private readonly TransactionDbContext _db;

    public TransactionRepository(TransactionDbContext db) => _db = db;

    public Task<bool> OrderReferenceExistsAsync(Guid tenantId, string orderReference, CancellationToken ct)
    {
        var trimmed = orderReference.Trim();
        return _db.Transactions.AnyAsync(
            t => t.TenantId == tenantId && t.OrderReference == trimmed,
            ct);
    }

    public Task<TransactionAggregate?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        return _db.Transactions.FirstOrDefaultAsync(
            t => t.TenantId == tenantId && t.Id == id,
            ct);
    }

    public async Task AddAsync(TransactionAggregate transaction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        await _db.Transactions.AddAsync(transaction, ct);
    }
}

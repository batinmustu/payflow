using Microsoft.EntityFrameworkCore;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Domain.Refunds;

namespace PayFlow.Transaction.Infrastructure.Persistence;

internal sealed class RefundRepository : IRefundRepository
{
    private readonly TransactionDbContext _db;

    public RefundRepository(TransactionDbContext db) => _db = db;

    public Task<Refund?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => _db.Set<Refund>().FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public async Task<long> SumOutstandingAmountAsync(Guid tenantId, Guid transactionId, CancellationToken ct)
    {
        return await _db.Set<Refund>()
            .Where(r => r.TenantId == tenantId
                     && r.TransactionId == transactionId
                     && r.State != RefundState.Failed)
            .SumAsync(r => r.AmountMinor, ct);
    }

    public async Task AddAsync(Refund refund, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(refund);
        await _db.Set<Refund>().AddAsync(refund, ct);
    }
}

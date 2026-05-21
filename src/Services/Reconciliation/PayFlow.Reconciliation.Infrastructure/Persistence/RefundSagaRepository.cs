using Microsoft.EntityFrameworkCore;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Domain.RefundSagas;

namespace PayFlow.Reconciliation.Infrastructure.Persistence;

internal sealed class RefundSagaRepository : IRefundSagaRepository
{
    private readonly ReconciliationDbContext _db;

    public RefundSagaRepository(ReconciliationDbContext db) => _db = db;

    public Task<RefundSaga?> FindByRefundIdAsync(Guid tenantId, Guid refundId, CancellationToken ct)
        => _db.Set<RefundSaga>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.RefundId == refundId, ct);

    public Task<RefundSaga?> GetAsync(Guid tenantId, Guid sagaId, CancellationToken ct)
        => _db.Set<RefundSaga>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == sagaId, ct);

    public async Task AddAsync(RefundSaga saga, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(saga);
        await _db.Set<RefundSaga>().AddAsync(saga, ct);
    }

    public async Task<IReadOnlyList<RefundSaga>> ListStuckProviderCalledAsync(
        DateTimeOffset staleSince,
        int maxAttempts,
        int take,
        CancellationToken ct)
    {
        return await _db.Set<RefundSaga>()
            .Where(s => s.State == RefundSagaState.ProviderCalled
                     && s.AttemptCount < maxAttempts
                     && s.StartedAt <= staleSince)
            .OrderBy(s => s.StartedAt)
            .Take(take)
            .ToListAsync(ct);
    }
}

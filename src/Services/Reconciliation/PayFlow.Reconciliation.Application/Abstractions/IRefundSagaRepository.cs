using PayFlow.Reconciliation.Domain.RefundSagas;

namespace PayFlow.Reconciliation.Application.Abstractions;

public interface IRefundSagaRepository
{
    /// <summary>Lookup by (tenant_id, refund_id) — the natural dedup key when a Kafka delivery replays.</summary>
    Task<RefundSaga?> FindByRefundIdAsync(Guid tenantId, Guid refundId, CancellationToken ct);

    Task<RefundSaga?> GetAsync(Guid tenantId, Guid sagaId, CancellationToken ct);

    Task AddAsync(RefundSaga saga, CancellationToken ct);
}

using PayFlow.Reconciliation.Domain.RefundSagas;

namespace PayFlow.Reconciliation.Application.Abstractions;

public interface IRefundSagaRepository
{
    /// <summary>Lookup by (tenant_id, refund_id) — the natural dedup key when a Kafka delivery replays.</summary>
    Task<RefundSaga?> FindByRefundIdAsync(Guid tenantId, Guid refundId, CancellationToken ct);

    Task<RefundSaga?> GetAsync(Guid tenantId, Guid sagaId, CancellationToken ct);

    Task AddAsync(RefundSaga saga, CancellationToken ct);

    /// <summary>
    /// Returns sagas stuck in <c>ProviderCalled</c> whose last activity is
    /// older than <paramref name="staleSince"/> and that still have retry
    /// budget left. The recovery worker drains this list and re-calls the
    /// provider — without it a transient failure leaves the saga in
    /// ProviderCalled forever (the original Kafka message is already
    /// processed, so redelivery wouldn't pick it up).
    /// </summary>
    Task<IReadOnlyList<RefundSaga>> ListStuckProviderCalledAsync(
        DateTimeOffset staleSince,
        int maxAttempts,
        int take,
        CancellationToken ct);
}

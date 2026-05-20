using PayFlow.Transaction.Domain.Refunds;

namespace PayFlow.Transaction.Application.Abstractions;

public interface IRefundRepository
{
    Task<Refund?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>
    /// Sums amounts of refunds that are not in a terminal-failed state — i.e.
    /// completed plus in-flight (Requested, Processing). Used to gate new
    /// refund requests against the remaining refundable balance so concurrent
    /// requests can't both pass when together they would exceed the capture.
    /// </summary>
    Task<long> SumOutstandingAmountAsync(Guid tenantId, Guid transactionId, CancellationToken ct);

    Task AddAsync(Refund refund, CancellationToken ct);
}

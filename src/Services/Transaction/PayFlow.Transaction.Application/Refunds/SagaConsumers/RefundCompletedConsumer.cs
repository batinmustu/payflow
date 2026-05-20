using Microsoft.Extensions.Logging;
using PayFlow.EventBus.Kafka.Consuming;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Domain.Refunds;

namespace PayFlow.Transaction.Application.Refunds.SagaConsumers;

/// <summary>
/// Terminal-success consumer for the refund saga. Updates the refund row to
/// <c>Completed</c> and walks the parent transaction's state machine from
/// <c>Captured</c>/<c>PartiallyRefunded</c> to <c>PartiallyRefunded</c>/<c>Refunded</c>
/// based on the new cumulative refunded amount. Idempotent on the refund's
/// terminal state.
/// </summary>
public sealed class RefundCompletedConsumer
    : IIntegrationEventConsumer<RefundCompletedIntegrationEvent>
{
    private readonly IRefundRepository _refunds;
    private readonly ITransactionRepository _transactions;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RefundCompletedConsumer> _logger;

    public RefundCompletedConsumer(
        IRefundRepository refunds,
        ITransactionRepository transactions,
        IUnitOfWork uow,
        ILogger<RefundCompletedConsumer> logger)
    {
        _refunds = refunds;
        _transactions = transactions;
        _uow = uow;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<RefundCompletedIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var p = envelope.Payload;

        var refund = await _refunds.GetAsync(p.TenantId, p.RefundId, ct);
        if (refund is null)
        {
            _logger.LogWarning(
                "RefundCompleted for unknown refund {RefundId}; skipping.", p.RefundId);
            return;
        }

        if (refund.State is RefundState.Completed or RefundState.Failed)
        {
            _logger.LogInformation(
                "Refund {RefundId} already terminal at {State}; ignoring duplicate completion.",
                p.RefundId, refund.State);
            return;
        }

        var transaction = await _transactions.GetAsync(p.TenantId, p.TransactionId, ct);
        if (transaction is null)
        {
            _logger.LogError(
                "RefundCompleted references unknown transaction {TransactionId}; skipping.",
                p.TransactionId);
            return;
        }

        refund.MarkCompleted(p.ProviderReference);
        transaction.ApplyRefundCompleted(refund.AmountMinor);

        await _uow.SaveChangesAsync(ct);
    }
}

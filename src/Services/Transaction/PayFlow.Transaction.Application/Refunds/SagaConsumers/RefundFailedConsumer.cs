using Microsoft.Extensions.Logging;
using PayFlow.EventBus.Kafka.Consuming;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Domain.Refunds;

namespace PayFlow.Transaction.Application.Refunds.SagaConsumers;

/// <summary>
/// Terminal-failure consumer. Flips the refund row to <c>Failed</c>; the
/// parent transaction stays in its current state so the captured amount
/// remains available for another refund attempt.
/// </summary>
public sealed class RefundFailedConsumer
    : IIntegrationEventConsumer<RefundFailedIntegrationEvent>
{
    private readonly IRefundRepository _refunds;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RefundFailedConsumer> _logger;

    public RefundFailedConsumer(
        IRefundRepository refunds,
        IUnitOfWork uow,
        ILogger<RefundFailedConsumer> logger)
    {
        _refunds = refunds;
        _uow = uow;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<RefundFailedIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var p = envelope.Payload;

        var refund = await _refunds.GetAsync(p.TenantId, p.RefundId, ct);
        if (refund is null)
        {
            _logger.LogWarning(
                "RefundFailed for unknown refund {RefundId}; skipping.", p.RefundId);
            return;
        }

        if (refund.State is RefundState.Completed or RefundState.Failed)
        {
            _logger.LogInformation(
                "Refund {RefundId} already terminal at {State}; ignoring duplicate failure.",
                p.RefundId, refund.State);
            return;
        }

        refund.MarkFailed(p.FailureReason);
        await _uow.SaveChangesAsync(ct);
    }
}

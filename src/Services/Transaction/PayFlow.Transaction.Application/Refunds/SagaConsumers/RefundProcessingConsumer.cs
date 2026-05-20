using Microsoft.Extensions.Logging;
using PayFlow.EventBus;
using PayFlow.Transaction.Application.Abstractions;
using PayFlow.Transaction.Domain.Refunds;

namespace PayFlow.Transaction.Application.Refunds.SagaConsumers;

/// <summary>
/// Reacts to <c>payflow.refund.processing.v1</c> from the saga: flips the
/// local refund record into <c>Processing</c>. Idempotent — if the row is
/// already in Processing/Completed/Failed (re-delivery, or saga events
/// arrived out of order), we log and skip.
/// </summary>
public sealed class RefundProcessingConsumer
    : IIntegrationEventConsumer<RefundProcessingIntegrationEvent>
{
    private readonly IRefundRepository _refunds;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RefundProcessingConsumer> _logger;

    public RefundProcessingConsumer(
        IRefundRepository refunds,
        IUnitOfWork uow,
        ILogger<RefundProcessingConsumer> logger)
    {
        _refunds = refunds;
        _uow = uow;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<RefundProcessingIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var p = envelope.Payload;

        var refund = await _refunds.GetAsync(p.TenantId, p.RefundId, ct);
        if (refund is null)
        {
            _logger.LogWarning(
                "RefundProcessing for unknown refund {RefundId} (tenant {TenantId}); skipping.",
                p.RefundId, p.TenantId);
            return;
        }

        if (refund.State != RefundState.Requested)
        {
            _logger.LogInformation(
                "Refund {RefundId} already in {State}; RefundProcessing is a no-op.",
                p.RefundId, refund.State);
            return;
        }

        refund.MarkProcessing();
        await _uow.SaveChangesAsync(ct);
    }
}

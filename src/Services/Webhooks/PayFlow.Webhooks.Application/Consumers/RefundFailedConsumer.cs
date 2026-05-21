using PayFlow.EventBus;
using PayFlow.Webhooks.Application.Deliveries;

namespace PayFlow.Webhooks.Application.Consumers;

public sealed class RefundFailedConsumer
    : IIntegrationEventConsumer<RefundFailedIntegrationEvent>
{
    private readonly WebhookDispatcher _dispatcher;
    public RefundFailedConsumer(WebhookDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task HandleAsync(
        IntegrationEventEnvelope<RefundFailedIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var p = envelope.Payload;
        var body = new
        {
            id = envelope.MessageId,
            eventType = envelope.EventType,
            occurredAt = (p.OccurredAt == default ? envelope.CreatedAt : p.OccurredAt),
            data = new
            {
                refundId = p.RefundId,
                transactionId = p.TransactionId,
                failureReason = p.FailureReason,
            },
        };
        return _dispatcher.EnqueueAsync(
            tenantId: p.TenantId,
            eventType: envelope.EventType,
            sourceMessageId: envelope.MessageId,
            payload: body,
            ct: ct);
    }
}

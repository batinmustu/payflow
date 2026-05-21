using PayFlow.EventBus;
using PayFlow.Webhooks.Application.Deliveries;

namespace PayFlow.Webhooks.Application.Consumers;

public sealed class TransactionCapturedConsumer
    : IIntegrationEventConsumer<TransactionCapturedIntegrationEvent>
{
    private readonly WebhookDispatcher _dispatcher;
    public TransactionCapturedConsumer(WebhookDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task HandleAsync(
        IntegrationEventEnvelope<TransactionCapturedIntegrationEvent> envelope,
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
                transactionId = p.TransactionId,
                providerCode = p.ProviderCode,
                providerReference = p.ProviderReference,
                amountMinor = p.AmountMinor,
                currency = p.Currency,
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

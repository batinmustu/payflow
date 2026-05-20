using System.Globalization;
using PayFlow.EventBus;
using PayFlow.Notification.Domain;

namespace PayFlow.Notification.Application.Notifications.Consumers;

public sealed class RefundCompletedConsumer
    : IIntegrationEventConsumer<RefundCompletedIntegrationEvent>
{
    private readonly NotificationDispatcher _dispatcher;
    public RefundCompletedConsumer(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task HandleAsync(
        IntegrationEventEnvelope<RefundCompletedIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var p = envelope.Payload;

        var model = new Dictionary<string, object?>
        {
            ["refundId"] = p.RefundId.ToString(),
            ["transactionId"] = p.TransactionId.ToString(),
            ["providerCode"] = p.ProviderCode,
            ["providerReference"] = p.ProviderReference,
            ["amount"] = (p.AmountMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = p.Currency,
            ["occurredAt"] = (p.OccurredAt == default ? envelope.CreatedAt : p.OccurredAt)
                .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
        };

        return _dispatcher.DispatchAsync(
            sourceMessageId: envelope.MessageId,
            sourceEventType: envelope.EventType,
            tenantId: p.TenantId,
            kind: NotificationKind.RefundCompleted,
            channel: NotificationChannel.Email,
            model: model,
            ct: ct);
    }
}

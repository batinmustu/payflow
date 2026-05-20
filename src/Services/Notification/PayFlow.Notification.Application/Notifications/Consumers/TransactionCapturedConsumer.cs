using System.Globalization;
using PayFlow.EventBus;
using PayFlow.Notification.Domain;

namespace PayFlow.Notification.Application.Notifications.Consumers;

public sealed class TransactionCapturedConsumer
    : IIntegrationEventConsumer<TransactionCapturedIntegrationEvent>
{
    private readonly NotificationDispatcher _dispatcher;
    public TransactionCapturedConsumer(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task HandleAsync(
        IntegrationEventEnvelope<TransactionCapturedIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var p = envelope.Payload;

        var model = new Dictionary<string, object?>
        {
            ["transactionId"] = p.TransactionId.ToString(),
            ["providerCode"] = p.ProviderCode,
            ["providerReference"] = p.ProviderReference,
            ["amount"] = FormatAmount(p.AmountMinor),
            ["currency"] = p.Currency,
            ["occurredAt"] = (p.OccurredAt == default ? envelope.CreatedAt : p.OccurredAt)
                .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
        };

        return _dispatcher.DispatchAsync(
            sourceMessageId: envelope.MessageId,
            sourceEventType: envelope.EventType,
            tenantId: p.TenantId,
            kind: NotificationKind.TransactionCaptured,
            channel: NotificationChannel.Email,
            model: model,
            ct: ct);
    }

    private static string FormatAmount(long minor) =>
        (minor / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}

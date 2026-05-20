using System.Globalization;
using PayFlow.EventBus;
using PayFlow.Notification.Domain;

namespace PayFlow.Notification.Application.Notifications.Consumers;

public sealed class RefundFailedConsumer
    : IIntegrationEventConsumer<RefundFailedIntegrationEvent>
{
    private readonly NotificationDispatcher _dispatcher;
    public RefundFailedConsumer(NotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public Task HandleAsync(
        IntegrationEventEnvelope<RefundFailedIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var p = envelope.Payload;

        var model = new Dictionary<string, object?>
        {
            ["refundId"] = p.RefundId.ToString(),
            ["transactionId"] = p.TransactionId.ToString(),
            ["failureReason"] = p.FailureReason,
            ["occurredAt"] = (p.OccurredAt == default ? envelope.CreatedAt : p.OccurredAt)
                .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
        };

        return _dispatcher.DispatchAsync(
            sourceMessageId: envelope.MessageId,
            sourceEventType: envelope.EventType,
            tenantId: p.TenantId,
            kind: NotificationKind.RefundFailed,
            channel: NotificationChannel.Email,
            model: model,
            ct: ct);
    }
}

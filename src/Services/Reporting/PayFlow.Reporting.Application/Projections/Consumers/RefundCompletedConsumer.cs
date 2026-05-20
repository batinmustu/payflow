using PayFlow.EventBus.Kafka.Consuming;

namespace PayFlow.Reporting.Application.Projections.Consumers;

public sealed class RefundCompletedConsumer
    : IIntegrationEventConsumer<RefundCompletedIntegrationEvent>
{
    private readonly SummaryProjectionService _projection;
    public RefundCompletedConsumer(SummaryProjectionService projection) => _projection = projection;

    public Task HandleAsync(
        IntegrationEventEnvelope<RefundCompletedIntegrationEvent> envelope,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var p = envelope.Payload;

        return _projection.ApplyAsync(
            messageId: envelope.MessageId,
            eventType: envelope.EventType,
            tenantId: p.TenantId,
            occurredAt: p.OccurredAt == default ? envelope.CreatedAt : p.OccurredAt,
            currency: p.Currency,
            apply: row => row.RecordRefunded(p.AmountMinor),
            ct: ct);
    }
}

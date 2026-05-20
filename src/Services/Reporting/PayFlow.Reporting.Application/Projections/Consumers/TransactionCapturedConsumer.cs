using PayFlow.EventBus.Kafka.Consuming;

namespace PayFlow.Reporting.Application.Projections.Consumers;

public sealed class TransactionCapturedConsumer
    : IIntegrationEventConsumer<TransactionCapturedIntegrationEvent>
{
    private readonly SummaryProjectionService _projection;
    public TransactionCapturedConsumer(SummaryProjectionService projection) => _projection = projection;

    public Task HandleAsync(
        IntegrationEventEnvelope<TransactionCapturedIntegrationEvent> envelope,
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
            apply: row => row.RecordCaptured(p.AmountMinor),
            ct: ct);
    }
}

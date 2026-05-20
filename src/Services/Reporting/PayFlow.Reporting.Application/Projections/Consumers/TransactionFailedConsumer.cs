using PayFlow.EventBus;

namespace PayFlow.Reporting.Application.Projections.Consumers;

public sealed class TransactionFailedConsumer
    : IIntegrationEventConsumer<TransactionFailedIntegrationEvent>
{
    private readonly SummaryProjectionService _projection;
    public TransactionFailedConsumer(SummaryProjectionService projection) => _projection = projection;

    public Task HandleAsync(
        IntegrationEventEnvelope<TransactionFailedIntegrationEvent> envelope,
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
            apply: row => row.RecordFailed(),
            ct: ct);
    }
}

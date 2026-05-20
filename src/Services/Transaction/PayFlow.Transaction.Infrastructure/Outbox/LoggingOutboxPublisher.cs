using Microsoft.Extensions.Logging;
using PayFlow.Outbox;

namespace PayFlow.Transaction.Infrastructure.Outbox;

/// <summary>
/// Placeholder publisher used until the Kafka publisher lands. Walks each
/// row through the success path so the background service drains the table,
/// and writes a structured log line so dev / Postman demos can observe the
/// outbox in action via Seq + Jaeger.
/// </summary>
internal sealed class LoggingOutboxPublisher : IOutboxPublisher
{
    private readonly ILogger<LoggingOutboxPublisher> _logger;

    public LoggingOutboxPublisher(ILogger<LoggingOutboxPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        _logger.LogInformation(
            "Outbox publish (stub): {EventType} tenant={TenantId} aggregate={AggregateType}/{AggregateId} payload={Payload}",
            message.EventType,
            message.TenantId,
            message.AggregateType,
            message.AggregateId,
            message.Payload);
        return Task.CompletedTask;
    }
}

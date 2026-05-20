namespace PayFlow.Outbox;

/// <summary>
/// Transport for outbox messages. Concrete implementations target a specific
/// broker (KafkaOutboxPublisher, etc.). The background service is the only
/// caller; consumers do not see this interface.
/// </summary>
public interface IOutboxPublisher
{
    /// <summary>
    /// Publish one outbox row. Throw on transport failure; the background
    /// service translates the throw into a Failed row + backoff schedule.
    /// </summary>
    Task PublishAsync(OutboxMessage message, CancellationToken ct);
}

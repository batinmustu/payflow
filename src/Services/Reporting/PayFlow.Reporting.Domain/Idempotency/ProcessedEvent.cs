namespace PayFlow.Reporting.Domain.Idempotency;

/// <summary>
/// Tracks which integration-event <c>message_id</c>s we've already projected
/// into the read model. Kafka delivers at-least-once; without this, a
/// replay would double-count.
/// </summary>
public sealed class ProcessedEvent
{
    public Guid MessageId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }

    private ProcessedEvent() { }

    public static ProcessedEvent Mark(Guid messageId, string eventType) => new()
    {
        MessageId = messageId,
        EventType = eventType,
        ProcessedAt = DateTimeOffset.UtcNow,
    };
}

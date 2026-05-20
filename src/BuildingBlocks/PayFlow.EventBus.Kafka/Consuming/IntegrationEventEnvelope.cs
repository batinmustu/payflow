namespace PayFlow.EventBus.Kafka.Consuming;

/// <summary>
/// What a handler sees when an event arrives. Carries the deserialised payload
/// plus the envelope metadata that <see cref="KafkaOutboxPublisher"/> writes
/// into the message headers — handlers should treat these as the source of
/// truth for idempotency (use <see cref="MessageId"/> as a dedup key, not
/// anything inside the payload).
/// </summary>
public sealed record IntegrationEventEnvelope<TPayload>(
    string EventType,
    Guid MessageId,
    Guid TenantId,
    Guid AggregateId,
    string AggregateType,
    DateTimeOffset CreatedAt,
    TPayload Payload,
    IReadOnlyDictionary<string, string> Headers)
    where TPayload : class;

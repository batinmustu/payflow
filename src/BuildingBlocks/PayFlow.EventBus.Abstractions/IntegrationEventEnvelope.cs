namespace PayFlow.EventBus;

/// <summary>
/// What a consumer sees when an event arrives. Carries the deserialised
/// payload plus the envelope metadata the transport layer extracted from
/// headers. Handlers should treat <see cref="MessageId"/> as the source of
/// truth for idempotency rather than anything inside the payload.
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

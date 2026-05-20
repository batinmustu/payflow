namespace PayFlow.EventBus.Kafka.Consuming;

/// <summary>
/// Consumer-side counterpart to the producer's outbox event. One consumer per
/// <c>(event_type, consuming-service)</c>. The implementation must be
/// idempotent — Kafka delivers at-least-once, and replay after a crash or
/// rebalance is expected. Use
/// <see cref="IntegrationEventEnvelope{TPayload}.MessageId"/> as the dedup
/// key.
/// </summary>
public interface IIntegrationEventConsumer<TPayload>
    where TPayload : class
{
    Task HandleAsync(IntegrationEventEnvelope<TPayload> envelope, CancellationToken ct);
}

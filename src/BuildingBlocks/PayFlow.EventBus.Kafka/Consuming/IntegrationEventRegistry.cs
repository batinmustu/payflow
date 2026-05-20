using Microsoft.Extensions.DependencyInjection;

namespace PayFlow.EventBus.Kafka.Consuming;

/// <summary>
/// Lookup from <c>event_type</c> (== Kafka topic) to the handler it should
/// dispatch into. Built up via <c>AddPayFlowKafkaConsumer&lt;TPayload, THandler&gt;</c>
/// in service startup; consumed by the background loop to pick a handler when
/// a message arrives.
/// </summary>
public sealed class IntegrationEventRegistry
{
    private readonly Dictionary<string, EventRegistration> _byEventType =
        new(StringComparer.Ordinal);

    /// <summary>Adds a binding. Last writer wins for the same event type.</summary>
    public void Register<TPayload, THandler>(string eventType)
        where TPayload : class
        where THandler : class, IIntegrationEventConsumer<TPayload>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        _byEventType[eventType] = new EventRegistration(
            EventType: eventType,
            PayloadType: typeof(TPayload),
            HandlerType: typeof(THandler),
            DispatchAsync: static (sp, envelope, ct) =>
            {
                var handler = sp.GetRequiredService<THandler>();
                return handler.HandleAsync((IntegrationEventEnvelope<TPayload>)envelope, ct);
            });
    }

    public IReadOnlyCollection<string> EventTypes => _byEventType.Keys;

    public EventRegistration? TryGet(string eventType) =>
        _byEventType.TryGetValue(eventType, out var reg) ? reg : null;
}

public sealed record EventRegistration(
    string EventType,
    Type PayloadType,
    Type HandlerType,
    Func<IServiceProvider, object, CancellationToken, Task> DispatchAsync);

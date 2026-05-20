using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PayFlow.EventBus.Kafka.Consuming;

/// <summary>
/// One per service: subscribes to every topic that has a registered handler
/// (see <see cref="IntegrationEventRegistry"/>), polls Kafka in a loop, and
/// dispatches each message into a fresh DI scope.
///
/// Offsets are stored + committed only after a handler returns successfully.
/// If a handler throws we log + advance anyway — the partition is not blocked.
/// A proper dead-letter shovel will land alongside the first retryable saga
/// step that needs it (see docs/flows/refund-saga.md).
/// </summary>
public sealed class KafkaConsumerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IntegrationEventRegistry _registry;
    private readonly KafkaOptions _kafka;
    private readonly KafkaConsumerOptions _consumerOptions;
    private readonly ILogger<KafkaConsumerBackgroundService> _logger;

    public KafkaConsumerBackgroundService(
        IServiceProvider services,
        IntegrationEventRegistry registry,
        IOptions<KafkaOptions> kafka,
        IOptions<KafkaConsumerOptions> consumerOptions,
        ILogger<KafkaConsumerBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(kafka);
        ArgumentNullException.ThrowIfNull(consumerOptions);

        _services = services;
        _registry = registry;
        _kafka = kafka.Value;
        _consumerOptions = consumerOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The polling loop below runs the synchronous Confluent consumer
        // (.Consume blocks the calling thread). Without this yield the whole
        // body runs to the first message before the host's StartAsync gets to
        // bind Kestrel — i.e. /health would never become reachable until
        // traffic arrived. Yielding once lets startup complete normally.
        await Task.Yield();

        if (_registry.EventTypes.Count == 0)
        {
            _logger.LogWarning(
                "KafkaConsumerBackgroundService starting with no registered handlers; loop will not run.");
            return;
        }

        var config = BuildConsumerConfig();
        using var consumer = new ConsumerBuilder<string, byte[]>(config)
            .SetErrorHandler((_, err) =>
                _logger.LogError("Kafka consumer error {Code}: {Reason}", err.Code, err.Reason))
            .Build();

        var topics = _registry.EventTypes.ToArray();
        consumer.Subscribe(topics);
        _logger.LogInformation(
            "Kafka consumer subscribed: group={GroupId} topics={Topics}",
            _consumerOptions.GroupId, string.Join(",", topics));

        var pollTimeout = TimeSpan.FromMilliseconds(_consumerOptions.PollTimeoutMilliseconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, byte[]>? result;
                try
                {
                    result = consumer.Consume(pollTimeout);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka Consume() failed: {Reason}", ex.Error.Reason);
                    continue;
                }
                catch (OperationCanceledException) { break; }

                if (result is null || result.IsPartitionEOF) continue;

                await DispatchAsync(result, stoppingToken);
                AdvanceOffset(consumer, result);
            }
        }
        finally
        {
            try { consumer.Close(); } catch { /* shutdown best-effort */ }
        }
    }

    private async Task DispatchAsync(
        ConsumeResult<string, byte[]> result,
        CancellationToken ct)
    {
        var headers = IntegrationEventEnvelopeFactory.ExtractHeaders(result.Message.Headers);

        if (!headers.TryGetValue("event_type", out var eventType) || string.IsNullOrEmpty(eventType))
        {
            _logger.LogWarning(
                "Skipping message on {Topic} p={Partition} o={Offset}: missing event_type header.",
                result.Topic, result.Partition.Value, result.Offset.Value);
            return;
        }

        var registration = _registry.TryGet(eventType);
        if (registration is null)
        {
            _logger.LogWarning(
                "No handler registered for event_type {EventType}; skipping ({Topic} o={Offset}).",
                eventType, result.Topic, result.Offset.Value);
            return;
        }

        object envelope;
        try
        {
            envelope = IntegrationEventEnvelopeFactory.Build(
                registration.PayloadType, result.Message.Value, headers);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogError(ex,
                "Failed to build envelope for {EventType} ({Topic} o={Offset}); skipping.",
                eventType, result.Topic, result.Offset.Value);
            return;
        }

        using var scope = _services.CreateScope();
        try
        {
            // The lambda inside EventRegistration knows TPayload at compile time,
            // so it can do the downcast and resolve the concrete handler.
            await registration.DispatchAsync(scope.ServiceProvider, envelope, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Handler {Handler} threw on {EventType} ({Topic} o={Offset}); advancing offset anyway.",
                registration.HandlerType.Name, eventType, result.Topic, result.Offset.Value);
        }
    }

    private void AdvanceOffset(IConsumer<string, byte[]> consumer, ConsumeResult<string, byte[]> result)
    {
        try { consumer.StoreOffset(result); }
        catch (KafkaException ex) { _logger.LogWarning(ex, "StoreOffset failed."); }

        try { consumer.Commit(result); }
        catch (KafkaException ex) { _logger.LogWarning(ex, "Commit failed."); }
    }

    private ConsumerConfig BuildConsumerConfig() => new()
    {
        BootstrapServers = _kafka.BootstrapServers,
        ClientId = _kafka.ClientId,
        GroupId = _consumerOptions.GroupId,
        AutoOffsetReset = ParseOffsetReset(_consumerOptions.AutoOffsetReset),
        EnableAutoCommit = false,
        EnableAutoOffsetStore = false,
        AllowAutoCreateTopics = false,
    };

    private static AutoOffsetReset ParseOffsetReset(string raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "latest" => Confluent.Kafka.AutoOffsetReset.Latest,
            "error" => Confluent.Kafka.AutoOffsetReset.Error,
            _ => Confluent.Kafka.AutoOffsetReset.Earliest,
        };
}

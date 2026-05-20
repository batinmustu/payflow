using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using PayFlow.EventBus.Kafka.Telemetry;

namespace PayFlow.EventBus.Kafka.Consuming;

/// <summary>
/// One per service: subscribes to every topic that has a registered handler
/// (see <see cref="IntegrationEventRegistry"/>), polls Kafka in a loop, and
/// dispatches each message into a fresh DI scope.
///
/// Offsets are stored + committed only after a handler returns successfully.
/// On handler exceptions the loop retries the same offset up to
/// <see cref="KafkaConsumerOptions.MaxHandlerRetries"/> times (with a
/// backoff) before logging an error and advancing — so a transient
/// DB/HTTP blip self-heals, while a true poison pill doesn't park the
/// partition forever. A proper dead-letter shovel will land alongside
/// the first retryable saga step that needs it (see
/// docs/flows/refund-saga.md).
/// </summary>
public sealed class KafkaConsumerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IntegrationEventRegistry _registry;
    private readonly KafkaOptions _kafka;
    private readonly KafkaConsumerOptions _consumerOptions;
    private readonly ILogger<KafkaConsumerBackgroundService> _logger;

    // Tracks how many times the same offset has thrown in a row. Lives in
    // memory only — on restart we replay (idempotent consumers handle it).
    private readonly Dictionary<TopicPartitionOffset, int> _retryCounts = new();

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

                var outcome = await DispatchAsync(result, stoppingToken);
                if (outcome == DispatchOutcome.RetrySameOffset)
                {
                    // Don't advance — Confluent gives us the same record on
                    // the next Consume() call by default (offsets only move
                    // when we StoreOffset/Commit).
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(_consumerOptions.RetryBackoffMilliseconds),
                            stoppingToken);
                    }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                AdvanceOffset(consumer, result);
                _retryCounts.Remove(result.TopicPartitionOffset);
            }
        }
        finally
        {
            try { consumer.Close(); } catch { /* shutdown best-effort */ }
        }
    }

    private enum DispatchOutcome { Advance, RetrySameOffset }

    private async Task<DispatchOutcome> DispatchAsync(
        ConsumeResult<string, byte[]> result,
        CancellationToken ct)
    {
        var headers = IntegrationEventEnvelopeFactory.ExtractHeaders(result.Message.Headers);

        if (!headers.TryGetValue("event_type", out var eventType) || string.IsNullOrEmpty(eventType))
        {
            _logger.LogWarning(
                "Skipping message on {Topic} p={Partition} o={Offset}: missing event_type header.",
                result.Topic, result.Partition.Value, result.Offset.Value);
            return DispatchOutcome.Advance;
        }

        var registration = _registry.TryGet(eventType);
        if (registration is null)
        {
            _logger.LogWarning(
                "No handler registered for event_type {EventType}; skipping ({Topic} o={Offset}).",
                eventType, result.Topic, result.Offset.Value);
            return DispatchOutcome.Advance;
        }

        object envelope;
        try
        {
            envelope = IntegrationEventEnvelopeFactory.Build(
                registration.PayloadType, result.Message.Value, headers);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Malformed payload — retrying won't help, advance past it.
            _logger.LogError(ex,
                "Failed to build envelope for {EventType} ({Topic} o={Offset}); skipping.",
                eventType, result.Topic, result.Offset.Value);
            return DispatchOutcome.Advance;
        }

        // Pull the producer's W3C trace context out of the headers so the
        // consumer span attaches to the right parent. Without this every
        // consume looks like a fresh trace in Jaeger.
        var parentContext = KafkaTelemetry.Extract(result.Message.Headers);
        Baggage.Current = parentContext.Baggage;

        using var activity = KafkaTelemetry.ConsumerSource.StartActivity(
            $"kafka.consume {eventType}",
            ActivityKind.Consumer,
            parentContext.ActivityContext);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.source.name", result.Topic);
        activity?.SetTag("messaging.kafka.partition", result.Partition.Value);
        activity?.SetTag("messaging.kafka.offset", result.Offset.Value);
        if (headers.TryGetValue("message_id", out var msgId)) activity?.SetTag("messaging.message.id", msgId);
        if (headers.TryGetValue("tenant_id", out var tid)) activity?.SetTag("payflow.tenant_id", tid);

        using var scope = _services.CreateScope();
        try
        {
            await registration.DispatchAsync(scope.ServiceProvider, envelope, ct);
            return DispatchOutcome.Advance;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            var attempts = _retryCounts.TryGetValue(result.TopicPartitionOffset, out var n) ? n + 1 : 1;
            _retryCounts[result.TopicPartitionOffset] = attempts;

            if (attempts < _consumerOptions.MaxHandlerRetries)
            {
                _logger.LogWarning(ex,
                    "Handler {Handler} threw on {EventType} ({Topic} o={Offset}); retry {Attempt}/{Max}.",
                    registration.HandlerType.Name, eventType,
                    result.Topic, result.Offset.Value, attempts, _consumerOptions.MaxHandlerRetries);
                return DispatchOutcome.RetrySameOffset;
            }

            _logger.LogError(ex,
                "Handler {Handler} threw {Attempt} times on {EventType} ({Topic} o={Offset}); giving up and advancing.",
                registration.HandlerType.Name, attempts, eventType, result.Topic, result.Offset.Value);
            return DispatchOutcome.Advance;
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

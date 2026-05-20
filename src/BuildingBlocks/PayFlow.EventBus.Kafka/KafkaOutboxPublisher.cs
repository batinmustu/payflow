using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayFlow.EventBus.Kafka.Telemetry;
using PayFlow.Outbox;

namespace PayFlow.EventBus.Kafka;

/// <summary>
/// <see cref="IOutboxPublisher"/> implementation that pushes each outbox row
/// onto its event-type topic in Kafka. Topic name == OutboxMessage.EventType
/// (e.g. <c>payflow.transaction.captured.v1</c>); partition key == TenantId.
/// </summary>
public sealed class KafkaOutboxPublisher : IOutboxPublisher, IAsyncDisposable, IDisposable
{
    private readonly IProducer<string, byte[]> _producer;
    private readonly TimeSpan _produceTimeout;
    private readonly ILogger<KafkaOutboxPublisher> _logger;

    public KafkaOutboxPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaOutboxPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var opts = options.Value;
        _logger = logger;
        _produceTimeout = TimeSpan.FromMilliseconds(opts.RequestTimeoutMilliseconds);

        var config = new ProducerConfig
        {
            BootstrapServers = opts.BootstrapServers,
            ClientId = opts.ClientId,
            Acks = Acks.All,             // wait for the broker to write before acking
            EnableIdempotence = true,    // safe retries on flaky network
            LingerMs = 5,                // tiny batch window — favours throughput without hurting latency
        };
        _producer = new ProducerBuilder<string, byte[]>(config).Build();
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Open a producer span so the consumer's span has a parent. The
        // header injection below is what carries the trace context across
        // the broker; the activity itself shows up in Jaeger as
        // "kafka.publish {topic}".
        using var activity = KafkaTelemetry.ProducerSource.StartActivity(
            $"kafka.publish {message.EventType}",
            ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", message.EventType);
        activity?.SetTag("messaging.message.id", message.Id.ToString("N"));
        activity?.SetTag("payflow.tenant_id", message.TenantId.ToString("N"));

        var headers = BuildHeaders(message);
        if (activity is not null)
        {
            KafkaTelemetry.Inject(activity, headers);
        }

        var msg = new Message<string, byte[]>
        {
            Key = message.TenantId.ToString("N"),
            Value = Encoding.UTF8.GetBytes(message.Payload),
            Headers = headers,
        };

        using var produceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        produceCts.CancelAfter(_produceTimeout);

        try
        {
            var delivery = await _producer.ProduceAsync(message.EventType, msg, produceCts.Token);
            _logger.LogDebug(
                "Outbox → Kafka: {EventType} partition={Partition} offset={Offset}",
                message.EventType, delivery.Partition.Value, delivery.Offset.Value);
            activity?.SetTag("messaging.kafka.partition", delivery.Partition.Value);
            activity?.SetTag("messaging.kafka.offset", delivery.Offset.Value);
        }
        catch (ProduceException<string, byte[]> ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Error.Reason);
            // Bubble up so the outbox worker marks the row Failed + schedules a retry.
            throw new InvalidOperationException(
                $"Kafka produce failed for {message.EventType}: {ex.Error.Reason}", ex);
        }
    }

    private static Headers BuildHeaders(OutboxMessage message)
    {
        var headers = new Headers
        {
            { "message_id", Encoding.UTF8.GetBytes(message.Id.ToString("N")) },
            { "event_type", Encoding.UTF8.GetBytes(message.EventType) },
            { "tenant_id", Encoding.UTF8.GetBytes(message.TenantId.ToString("N")) },
            { "aggregate_type", Encoding.UTF8.GetBytes(message.AggregateType) },
            { "aggregate_id", Encoding.UTF8.GetBytes(message.AggregateId.ToString("N")) },
            { "created_at", Encoding.UTF8.GetBytes(message.CreatedAt.ToString("O")) },
        };

        // Fold whatever the producer set in the outbox row's `headers` blob
        // (correlation_id, causation_id, schema_version, ...).
        try
        {
            using var doc = JsonDocument.Parse(message.Headers);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    headers.Add(prop.Name, Encoding.UTF8.GetBytes(prop.Value.ToString()));
                }
            }
        }
        catch (JsonException)
        {
            // Malformed header blob — propagate as a single header so downstream
            // tooling sees it, rather than failing the whole publish.
            headers.Add("headers_parse_error", Encoding.UTF8.GetBytes("true"));
        }

        return headers;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Run(() => _producer.Flush(TimeSpan.FromSeconds(5)));
        _producer.Dispose();
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}

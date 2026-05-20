namespace PayFlow.EventBus.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Comma-separated <c>host:port</c> list of brokers.</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>How long the producer waits for an in-flight Produce before treating it as failed.</summary>
    public int RequestTimeoutMilliseconds { get; set; } = 5_000;

    /// <summary>
    /// Producer client.id. Surfaces on the broker side as the connection name —
    /// helps when reading lag metrics or kafka-console-consumer output.
    /// </summary>
    public string ClientId { get; set; } = "payflow";
}

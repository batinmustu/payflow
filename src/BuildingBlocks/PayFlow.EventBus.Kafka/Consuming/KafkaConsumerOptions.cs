namespace PayFlow.EventBus.Kafka.Consuming;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "Kafka:Consumer";

    /// <summary>
    /// Consumer group id. One group per service (e.g. <c>reconciliation</c>);
    /// multiple instances of the same service share the group and divide
    /// partitions among themselves.
    /// </summary>
    public string GroupId { get; set; } = "payflow";

    /// <summary>
    /// Where to start when a fresh group has no committed offset. Default is
    /// <c>earliest</c> so a new service catches up on history; flip to
    /// <c>latest</c> for ephemeral consumers that should only see new traffic.
    /// </summary>
    public string AutoOffsetReset { get; set; } = "earliest";

    /// <summary>How long <c>Consume()</c> blocks each iteration before returning null.</summary>
    public int PollTimeoutMilliseconds { get; set; } = 1_000;
}

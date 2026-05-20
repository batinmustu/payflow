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

    /// <summary>
    /// How many times the same Kafka offset gets retried after a handler
    /// exception before we give up and advance past it. Each retry pauses
    /// for <see cref="RetryBackoffMilliseconds"/> before the consumer sees
    /// the same record again. Once the budget is exhausted we log an error
    /// and move on so a single poison pill doesn't park a partition forever.
    /// (Replace with a real dead-letter shovel when one lands.)
    /// </summary>
    public int MaxHandlerRetries { get; set; } = 3;

    /// <summary>Backoff between retries of the same offset, in milliseconds.</summary>
    public int RetryBackoffMilliseconds { get; set; } = 500;
}

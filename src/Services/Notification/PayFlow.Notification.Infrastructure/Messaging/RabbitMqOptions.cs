namespace PayFlow.Notification.Infrastructure.Messaging;

/// <summary>
/// Binds the <c>RabbitMQ</c> configuration section. When
/// <see cref="ConnectionString"/> is null/empty, the Notification service
/// degrades to the in-memory <c>NullNotificationRetryQueue</c> and no retry
/// background consumer is started — useful for unit tests and for local dev
/// that doesn't want the broker container running.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    /// <summary>AMQP URI (e.g. <c>amqp://guest:guest@localhost:5672</c>).</summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Direct exchange the dispatcher publishes failed-send retries to.
    /// </summary>
    public string RetryExchange { get; set; } = "payflow.notification.retry";

    /// <summary>
    /// Holding queue with per-message TTL + DLX. Messages sit here until
    /// their TTL elapses, then get dead-lettered to <see cref="WorkQueue"/>.
    /// </summary>
    public string WaitQueue { get; set; } = "payflow.notification.retry.wait";

    /// <summary>The consumer-readable queue. Each message is a retry attempt.</summary>
    public string WorkQueue { get; set; } = "payflow.notification.retry.work";

    /// <summary>Dead-letter parking lot for notifications that exhausted MaxAttempts.</summary>
    public string DlqQueue { get; set; } = "payflow.notification.retry.dlq";

    /// <summary>
    /// Per-attempt backoff in milliseconds. Index = attempt number being
    /// scheduled, 1-based; index 0 unused. Defaults to 5s, 15s, 45s, 135s
    /// (~2 min), 405s (~7 min) — exponential 3× with attempt 1 at 5s.
    /// </summary>
    public int[] BackoffMillisecondsByAttempt { get; set; } =
        new[] { 0, 5_000, 15_000, 45_000, 135_000, 405_000 };

    public int BackoffFor(int attempt)
    {
        if (attempt <= 0) return 0;
        var table = BackoffMillisecondsByAttempt;
        if (table.Length == 0) return 5_000;
        return attempt < table.Length ? table[attempt] : table[^1];
    }
}

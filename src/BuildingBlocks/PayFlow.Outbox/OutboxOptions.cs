namespace PayFlow.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Polling interval when the previous tick found work. Default 500ms per ADR-0004.</summary>
    public int PollIntervalMilliseconds { get; set; } = 500;

    /// <summary>Max rows claimed per tick. Default 100 per docs/database/outbox.md.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>How long a Publishing row may sit before the recovery sweep resets it. Default 60s.</summary>
    public int StuckPublishingThresholdSeconds { get; set; } = 60;

    /// <summary>
    /// How often the stuck-Publishing sweeper runs while the worker is alive
    /// (the same query also runs once on startup). Default 60s.
    /// </summary>
    public int StuckSweepIntervalSeconds { get; set; } = 60;

    /// <summary>Max attempts before a row goes terminal (alerted; not retried automatically).</summary>
    public int MaxAttempts { get; set; } = 7;
}

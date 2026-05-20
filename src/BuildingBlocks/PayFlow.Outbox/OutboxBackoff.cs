namespace PayFlow.Outbox;

/// <summary>
/// Backoff schedule from docs/database/outbox.md.
///   attempt 1→2 = 5s
///   attempt 2→3 = 30s
///   attempt 3→4 = 2m
///   attempt 4→5 = 10m
///   attempt 5→6 = 1h
///   attempt 6→7 = 6h
///   attempt 7+  = terminal (caller alerts; no auto-retry)
/// </summary>
public static class OutboxBackoff
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
    ];

    public static TimeSpan NextDelayFor(int previousAttempts)
    {
        var index = Math.Clamp(previousAttempts, 0, Delays.Length - 1);
        return Delays[index];
    }

    public static bool IsTerminal(int attemptCount, int maxAttempts) =>
        attemptCount >= maxAttempts;
}

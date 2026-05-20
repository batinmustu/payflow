namespace PayFlow.Transaction.API.Idempotency;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Transaction:Idempotency";

    public string RedisConnectionString { get; set; } = "localhost:6380";

    /// <summary>How long a stored response stays replayable. Default 24h per docs/api/idempotency.md.</summary>
    public int TtlHours { get; set; } = 24;
}

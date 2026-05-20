namespace PayFlow.Outbox;

/// <summary>
/// A pending or published integration event row. Schema and lifecycle match
/// docs/database/outbox.md exactly — same columns, same state machine, same
/// indices. Owned by whichever service writes the row; the
/// <see cref="OutboxBackgroundService"/> drains it.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string AggregateType { get; private set; }
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public string Headers { get; private set; }
    public OutboxMessageState State { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    private OutboxMessage(
        Guid id,
        Guid tenantId,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string payload,
        string headers,
        OutboxMessageState state,
        DateTimeOffset createdAt,
        DateTimeOffset nextAttemptAt)
    {
        Id = id;
        TenantId = tenantId;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        EventType = eventType;
        Payload = payload;
        Headers = headers;
        State = state;
        CreatedAt = createdAt;
        NextAttemptAt = nextAttemptAt;
    }

    public static OutboxMessage Create(
        Guid tenantId,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string payload,
        string headers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var now = DateTimeOffset.UtcNow;
        return new OutboxMessage(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            aggregateType: aggregateType,
            aggregateId: aggregateId,
            eventType: eventType,
            payload: payload,
            headers: headers ?? "{}",
            state: OutboxMessageState.Pending,
            createdAt: now,
            nextAttemptAt: now);
    }

    public void MarkPublishing() => State = OutboxMessageState.Publishing;

    public void MarkPublished()
    {
        State = OutboxMessageState.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public void MarkFailed(string error, TimeSpan nextAttemptDelay)
    {
        State = OutboxMessageState.Failed;
        LastError = error;
        AttemptCount++;
        NextAttemptAt = DateTimeOffset.UtcNow.Add(nextAttemptDelay);
    }

    /// <summary>
    /// Recovery sweep: a row stuck in <see cref="OutboxMessageState.Publishing"/>
    /// for longer than the threshold gets returned to <see cref="OutboxMessageState.Pending"/>
    /// without consuming a retry, so a crashed publisher does not eat the budget.
    /// </summary>
    public void ResetPublishingToPending()
    {
        if (State != OutboxMessageState.Publishing) return;
        State = OutboxMessageState.Pending;
        NextAttemptAt = DateTimeOffset.UtcNow;
    }
}

public enum OutboxMessageState
{
    Pending = 0,
    Publishing = 1,
    Published = 2,
    Failed = 3,
}

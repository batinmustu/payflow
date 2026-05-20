using PayFlow.SharedKernel;

namespace PayFlow.Notification.Domain.Notifications;

/// <summary>
/// One row per outgoing notification (one event may produce multiple — e.g.
/// an email + an SMS). The body is rendered ahead of time so the audit trail
/// shows exactly what the merchant received; the provider call only happens
/// after the row exists (so a crash before send still leaves a Pending
/// record that a recovery worker can pick up).
/// </summary>
public sealed class NotificationRecord : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public NotificationKind Kind { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    /// <summary>Kafka message_id that triggered this notification — dedup key.</summary>
    public Guid SourceMessageId { get; private set; }
    public string SourceEventType { get; private set; } = string.Empty;

    public NotificationState State { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ProviderReference { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }

    private NotificationRecord() { }

    public static Result<NotificationRecord> Create(
        Guid tenantId,
        NotificationKind kind,
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        Guid sourceMessageId,
        string sourceEventType)
    {
        if (tenantId == Guid.Empty) return Result.Failure<NotificationRecord>("TENANT_REQUIRED");
        if (string.IsNullOrWhiteSpace(recipient)) return Result.Failure<NotificationRecord>("RECIPIENT_REQUIRED");
        if (string.IsNullOrWhiteSpace(subject)) return Result.Failure<NotificationRecord>("SUBJECT_REQUIRED");
        if (string.IsNullOrWhiteSpace(body)) return Result.Failure<NotificationRecord>("BODY_REQUIRED");
        if (sourceMessageId == Guid.Empty) return Result.Failure<NotificationRecord>("SOURCE_MESSAGE_REQUIRED");
        if (string.IsNullOrWhiteSpace(sourceEventType)) return Result.Failure<NotificationRecord>("SOURCE_EVENT_TYPE_REQUIRED");

        return Result.Success(new NotificationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = kind,
            Channel = channel,
            Recipient = recipient.Trim(),
            Subject = subject.Trim(),
            Body = body,
            SourceMessageId = sourceMessageId,
            SourceEventType = sourceEventType.Trim(),
            State = NotificationState.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    public void MarkSent(string? providerReference)
    {
        EnsureState(NotificationState.Pending);
        State = NotificationState.Sent;
        ProviderReference = providerReference;
        AttemptCount++;
        SentAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string failureReason)
    {
        EnsureState(NotificationState.Pending);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);
        State = NotificationState.Failed;
        FailureReason = failureReason;
        AttemptCount++;
        FailedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureState(NotificationState expected)
    {
        if (State != expected)
        {
            throw new InvalidOperationException(
                $"Notification {Id} is in state {State}; expected {expected}.");
        }
    }
}

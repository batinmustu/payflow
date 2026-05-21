using PayFlow.SharedKernel;

namespace PayFlow.Webhooks.Domain.Deliveries;

/// <summary>
/// One row per (subscription, source event) — the audit trail of what we
/// POSTed where and what happened. Idempotency comes from the
/// (SubscriptionId, SourceMessageId) unique index: a redelivered Kafka
/// message lands an existence-check skip in the consumer rather than a
/// duplicate POST.
///
/// State machine:
///   Pending --MarkSent--> Sent              (terminal success)
///   Pending --MarkTransientFailure--> Pending  (retry sweeper will pick it up)
///   Pending --MarkFailed--> Failed          (budget exhausted)
/// </summary>
public sealed class WebhookDelivery : AggregateRoot<Guid>
{
    public const int MaxAttempts = 5;

    public Guid TenantId { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Kafka message id that triggered this delivery — dedup key.</summary>
    public Guid SourceMessageId { get; private set; }

    public string TargetUrl { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string Signature { get; private set; } = string.Empty;

    public WebhookDeliveryState State { get; private set; }
    public int AttemptCount { get; private set; }
    public int? LastStatusCode { get; private set; }
    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }

    private WebhookDelivery() { }

    public static Result<WebhookDelivery> Create(
        Guid tenantId,
        Guid subscriptionId,
        string eventType,
        Guid sourceMessageId,
        string targetUrl,
        string payload,
        string signature)
    {
        if (tenantId == Guid.Empty) return Result.Failure<WebhookDelivery>("TENANT_REQUIRED");
        if (subscriptionId == Guid.Empty) return Result.Failure<WebhookDelivery>("SUBSCRIPTION_REQUIRED");
        if (sourceMessageId == Guid.Empty) return Result.Failure<WebhookDelivery>("SOURCE_MESSAGE_REQUIRED");
        if (string.IsNullOrWhiteSpace(eventType)) return Result.Failure<WebhookDelivery>("EVENT_TYPE_REQUIRED");
        if (string.IsNullOrWhiteSpace(targetUrl)) return Result.Failure<WebhookDelivery>("URL_REQUIRED");
        if (string.IsNullOrWhiteSpace(payload)) return Result.Failure<WebhookDelivery>("PAYLOAD_REQUIRED");
        if (string.IsNullOrWhiteSpace(signature)) return Result.Failure<WebhookDelivery>("SIGNATURE_REQUIRED");

        var now = DateTimeOffset.UtcNow;
        return Result.Success(new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            EventType = eventType,
            SourceMessageId = sourceMessageId,
            TargetUrl = targetUrl,
            Payload = payload,
            Signature = signature,
            State = WebhookDeliveryState.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }

    public void MarkSent(int statusCode)
    {
        EnsureState(WebhookDeliveryState.Pending);
        State = WebhookDeliveryState.Sent;
        LastStatusCode = statusCode;
        LastError = null;
        AttemptCount++;
        SentAt = DateTimeOffset.UtcNow;
        NextAttemptAt = null;
    }

    public void MarkFailed(int? statusCode, string error)
    {
        EnsureState(WebhookDeliveryState.Pending);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        State = WebhookDeliveryState.Failed;
        LastStatusCode = statusCode;
        LastError = error;
        AttemptCount++;
        FailedAt = DateTimeOffset.UtcNow;
        NextAttemptAt = null;
    }

    public void MarkTransientFailure(int? statusCode, string error, TimeSpan backoff)
    {
        EnsureState(WebhookDeliveryState.Pending);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastStatusCode = statusCode;
        LastError = error;
        AttemptCount++;
        NextAttemptAt = DateTimeOffset.UtcNow + backoff;
    }

    public bool CanRetry =>
        State == WebhookDeliveryState.Pending && AttemptCount < MaxAttempts;

    private void EnsureState(WebhookDeliveryState expected)
    {
        if (State != expected)
        {
            throw new InvalidOperationException(
                $"WebhookDelivery {Id} is in state {State}; expected {expected}.");
        }
    }
}

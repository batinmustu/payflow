using PayFlow.SharedKernel;

namespace PayFlow.Webhooks.Domain.Subscriptions;

/// <summary>
/// A merchant's standing instruction "POST these event types to this URL,
/// signed with this secret". One subscription per (tenant, event type, URL)
/// — the same merchant can subscribe multiple URLs to the same event.
///
/// The secret is generated at create time (32 bytes, base64) and never
/// exposed by the API again after creation. Disable via <see cref="Deactivate"/>
/// when the merchant rotates URLs or revokes the integration; we keep the
/// row for the audit trail rather than deleting.
/// </summary>
public sealed class WebhookSubscription : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string Secret { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    private WebhookSubscription() { }

    public static Result<WebhookSubscription> Create(
        Guid tenantId,
        string eventType,
        string url,
        string secret)
    {
        if (tenantId == Guid.Empty) return Result.Failure<WebhookSubscription>("TENANT_REQUIRED");
        if (string.IsNullOrWhiteSpace(eventType)) return Result.Failure<WebhookSubscription>("EVENT_TYPE_REQUIRED");
        if (string.IsNullOrWhiteSpace(url)) return Result.Failure<WebhookSubscription>("URL_REQUIRED");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return Result.Failure<WebhookSubscription>("URL_INVALID");
        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
            return Result.Failure<WebhookSubscription>("URL_SCHEME_UNSUPPORTED");
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            return Result.Failure<WebhookSubscription>("SECRET_TOO_SHORT");

        return Result.Success(new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType.Trim(),
            Url = url.Trim(),
            Secret = secret,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        DeactivatedAt = DateTimeOffset.UtcNow;
    }
}

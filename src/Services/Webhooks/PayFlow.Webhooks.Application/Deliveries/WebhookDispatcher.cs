using System.Text.Json;
using Microsoft.Extensions.Logging;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Domain.Deliveries;

namespace PayFlow.Webhooks.Application.Deliveries;

/// <summary>
/// Two entry points:
/// <list type="bullet">
///   <item><see cref="EnqueueAsync"/> — invoked by the Kafka consumers
///         on event arrival. Fans the event out to every active
///         subscription, creates one delivery row per fan-out, attempts
///         delivery, and either marks Sent or schedules a retry.</item>
///   <item><see cref="RedispatchAsync"/> — invoked by the retry sweeper.
///         Re-loads one delivery and tries again with the same payload +
///         signature (idempotency at the merchant's end depends on the
///         delivery id, included in the body).</item>
/// </list>
///
/// The retry-vs-terminal decision lives here so both paths agree:
///   4xx (except 408/429) — terminal (merchant told us we're wrong)
///   5xx / transport     — transient, schedule next retry
///   exhausted MaxAttempts — terminal Failed
/// </summary>
public sealed class WebhookDispatcher
{
    private static readonly TimeSpan[] Backoffs =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IWebhookSubscriptionRepository _subscriptions;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IPayloadSigner _signer;
    private readonly IWebhookHttpClient _http;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        IWebhookSubscriptionRepository subscriptions,
        IWebhookDeliveryRepository deliveries,
        IPayloadSigner signer,
        IWebhookHttpClient http,
        IUnitOfWork uow,
        ILogger<WebhookDispatcher> logger)
    {
        _subscriptions = subscriptions;
        _deliveries = deliveries;
        _signer = signer;
        _http = http;
        _uow = uow;
        _logger = logger;
    }

    public async Task EnqueueAsync(
        Guid tenantId,
        string eventType,
        Guid sourceMessageId,
        object payload,
        CancellationToken ct)
    {
        var subs = await _subscriptions.ListActiveForEventAsync(tenantId, eventType, ct);
        if (subs.Count == 0)
        {
            _logger.LogDebug(
                "No active subscriptions for {EventType} on tenant {TenantId}; skipping.",
                eventType, tenantId);
            return;
        }

        var body = JsonSerializer.Serialize(payload, JsonOptions);

        foreach (var sub in subs)
        {
            if (await _deliveries.ExistsForAsync(sub.Id, sourceMessageId, ct))
            {
                _logger.LogInformation(
                    "Delivery for subscription {SubId} + source {MessageId} already exists; skipping.",
                    sub.Id, sourceMessageId);
                continue;
            }

            var signature = _signer.Sign(body, sub.Secret);
            var deliveryResult = WebhookDelivery.Create(
                tenantId: tenantId,
                subscriptionId: sub.Id,
                eventType: eventType,
                sourceMessageId: sourceMessageId,
                targetUrl: sub.Url,
                payload: body,
                signature: signature);

            if (deliveryResult.IsFailure)
            {
                _logger.LogError(
                    "Could not create delivery for sub {SubId}: {Error}",
                    sub.Id, deliveryResult.ErrorCode);
                continue;
            }

            var delivery = deliveryResult.Value;
            await _deliveries.AddAsync(delivery, ct);
            await _uow.SaveChangesAsync(ct);

            await AttemptDeliveryAsync(delivery, ct);
        }
    }

    public async Task RedispatchAsync(Guid deliveryId, CancellationToken ct)
    {
        var delivery = await _deliveries.GetAsync(deliveryId, ct);
        if (delivery is null)
        {
            _logger.LogWarning("Retry: delivery {DeliveryId} not found; dropping.", deliveryId);
            return;
        }
        if (delivery.State != WebhookDeliveryState.Pending)
        {
            _logger.LogDebug(
                "Retry: delivery {DeliveryId} is {State}; nothing to do.",
                deliveryId, delivery.State);
            return;
        }

        await AttemptDeliveryAsync(delivery, ct);
    }

    private async Task AttemptDeliveryAsync(WebhookDelivery delivery, CancellationToken ct)
    {
        var result = await _http.PostAsync(
            delivery.TargetUrl, delivery.Payload, delivery.Signature, ct);

        if (result.IsSuccess)
        {
            delivery.MarkSent(result.StatusCode!.Value);
            await _uow.SaveChangesAsync(ct);
            return;
        }

        var attemptedNow = delivery.AttemptCount + 1;

        // 4xx that aren't 408/429 are merchant-side problems we won't fix
        // by retrying — bail immediately.
        if (result.IsTerminal4xx)
        {
            delivery.MarkFailed(result.StatusCode, result.Error ?? $"HTTP_{result.StatusCode}");
            await _uow.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Delivery {DeliveryId} got terminal {Status}; marked Failed.",
                delivery.Id, result.StatusCode);
            return;
        }

        if (attemptedNow >= WebhookDelivery.MaxAttempts)
        {
            delivery.MarkFailed(result.StatusCode, result.Error ?? "MAX_ATTEMPTS_EXHAUSTED");
            await _uow.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Delivery {DeliveryId} exhausted retry budget ({Attempts}/{Max}).",
                delivery.Id, attemptedNow, WebhookDelivery.MaxAttempts);
            return;
        }

        var backoff = Backoffs[Math.Min(delivery.AttemptCount, Backoffs.Length - 1)];
        delivery.MarkTransientFailure(
            result.StatusCode,
            result.Error ?? $"HTTP_{result.StatusCode}",
            backoff);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Delivery {DeliveryId} attempt {Attempt}/{Max} failed ({Reason}); next retry in {Backoff}.",
            delivery.Id, attemptedNow, WebhookDelivery.MaxAttempts,
            result.Error ?? $"HTTP_{result.StatusCode}", backoff);
    }
}

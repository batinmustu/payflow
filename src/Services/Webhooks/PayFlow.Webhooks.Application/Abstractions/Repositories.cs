using PayFlow.Webhooks.Domain.Deliveries;
using PayFlow.Webhooks.Domain.Subscriptions;

namespace PayFlow.Webhooks.Application.Abstractions;

public interface IWebhookSubscriptionRepository
{
    Task<IReadOnlyList<WebhookSubscription>> ListActiveForEventAsync(
        Guid tenantId, string eventType, CancellationToken ct);

    Task<IReadOnlyList<WebhookSubscription>> ListForTenantAsync(
        Guid tenantId, CancellationToken ct);

    Task<WebhookSubscription?> GetAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct);

    Task AddAsync(WebhookSubscription subscription, CancellationToken ct);
}

public interface IWebhookDeliveryRepository
{
    Task<bool> ExistsForAsync(Guid subscriptionId, Guid sourceMessageId, CancellationToken ct);

    Task<WebhookDelivery?> GetAsync(Guid deliveryId, CancellationToken ct);

    Task<IReadOnlyList<WebhookDelivery>> ListPendingRetriesAsync(
        DateTimeOffset dueBefore, int maxAttempts, int take, CancellationToken ct);

    Task<IReadOnlyList<WebhookDelivery>> ListForTenantAsync(
        Guid tenantId, int take, CancellationToken ct);

    Task AddAsync(WebhookDelivery delivery, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}

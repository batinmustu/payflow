using Microsoft.EntityFrameworkCore;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Domain.Subscriptions;

namespace PayFlow.Webhooks.Infrastructure.Persistence;

internal sealed class SubscriptionRepository : IWebhookSubscriptionRepository
{
    private readonly WebhooksDbContext _db;
    public SubscriptionRepository(WebhooksDbContext db) => _db = db;

    public async Task<IReadOnlyList<WebhookSubscription>> ListActiveForEventAsync(
        Guid tenantId, string eventType, CancellationToken ct) =>
        await _db.Subscriptions
            .Where(s => s.TenantId == tenantId && s.EventType == eventType && s.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WebhookSubscription>> ListForTenantAsync(
        Guid tenantId, CancellationToken ct) =>
        await _db.Subscriptions
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public Task<WebhookSubscription?> GetAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct) =>
        _db.Subscriptions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.Id == subscriptionId, ct);

    public async Task AddAsync(WebhookSubscription subscription, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await _db.Subscriptions.AddAsync(subscription, ct);
    }
}

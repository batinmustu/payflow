using Microsoft.EntityFrameworkCore;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Domain.Deliveries;

namespace PayFlow.Webhooks.Infrastructure.Persistence;

internal sealed class DeliveryRepository : IWebhookDeliveryRepository
{
    private readonly WebhooksDbContext _db;
    public DeliveryRepository(WebhooksDbContext db) => _db = db;

    public Task<bool> ExistsForAsync(Guid subscriptionId, Guid sourceMessageId, CancellationToken ct) =>
        _db.Deliveries.AnyAsync(
            d => d.SubscriptionId == subscriptionId && d.SourceMessageId == sourceMessageId, ct);

    public Task<WebhookDelivery?> GetAsync(Guid deliveryId, CancellationToken ct) =>
        _db.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct);

    public async Task<IReadOnlyList<WebhookDelivery>> ListPendingRetriesAsync(
        DateTimeOffset dueBefore, int maxAttempts, int take, CancellationToken ct) =>
        await _db.Deliveries
            .Where(d => d.State == WebhookDeliveryState.Pending
                     && d.AttemptCount < maxAttempts
                     && d.NextAttemptAt != null
                     && d.NextAttemptAt <= dueBefore)
            .OrderBy(d => d.NextAttemptAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WebhookDelivery>> ListForTenantAsync(
        Guid tenantId, int take, CancellationToken ct) =>
        await _db.Deliveries
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(WebhookDelivery delivery, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        await _db.Deliveries.AddAsync(delivery, ct);
    }
}

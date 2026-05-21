using MediatR;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Domain.Deliveries;

namespace PayFlow.Webhooks.Application.Deliveries;

public sealed record ListDeliveriesQuery(Guid TenantId, int Take = 100)
    : IRequest<IReadOnlyList<DeliveryListItem>>;

public sealed record DeliveryListItem(
    Guid Id,
    Guid SubscriptionId,
    string EventType,
    string TargetUrl,
    WebhookDeliveryState State,
    int AttemptCount,
    int? LastStatusCode,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? SentAt);

internal sealed class ListDeliveriesHandler
    : IRequestHandler<ListDeliveriesQuery, IReadOnlyList<DeliveryListItem>>
{
    private readonly IWebhookDeliveryRepository _repo;
    public ListDeliveriesHandler(IWebhookDeliveryRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<DeliveryListItem>> Handle(
        ListDeliveriesQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = await _repo.ListForTenantAsync(request.TenantId, Math.Clamp(request.Take, 1, 500), ct);
        return rows.Select(d => new DeliveryListItem(
            d.Id, d.SubscriptionId, d.EventType, d.TargetUrl, d.State,
            d.AttemptCount, d.LastStatusCode, d.LastError,
            d.CreatedAt, d.NextAttemptAt, d.SentAt)).ToList();
    }
}

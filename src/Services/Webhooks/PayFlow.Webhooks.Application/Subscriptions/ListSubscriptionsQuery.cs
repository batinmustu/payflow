using MediatR;
using PayFlow.Webhooks.Application.Abstractions;

namespace PayFlow.Webhooks.Application.Subscriptions;

public sealed record ListSubscriptionsQuery(Guid TenantId)
    : IRequest<IReadOnlyList<SubscriptionListItem>>;

public sealed record SubscriptionListItem(
    Guid Id,
    string EventType,
    string Url,
    bool IsActive,
    DateTimeOffset CreatedAt);
// Secret deliberately omitted — see CreateSubscriptionHandler note.

internal sealed class ListSubscriptionsHandler
    : IRequestHandler<ListSubscriptionsQuery, IReadOnlyList<SubscriptionListItem>>
{
    private readonly IWebhookSubscriptionRepository _repo;
    public ListSubscriptionsHandler(IWebhookSubscriptionRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<SubscriptionListItem>> Handle(
        ListSubscriptionsQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = await _repo.ListForTenantAsync(request.TenantId, ct);
        return rows
            .Select(s => new SubscriptionListItem(s.Id, s.EventType, s.Url, s.IsActive, s.CreatedAt))
            .ToList();
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayFlow.Transaction.Application.Abstractions;

namespace PayFlow.Transaction.Infrastructure.Routing;

/// <summary>
/// v1 — every tenant gets the same ordered provider list, loaded from
/// <c>Transaction:Routing:Providers</c>. The handler walks the list,
/// falling over on soft declines, stopping on hard declines or success.
/// Per-tenant routing rules land later from a routing_rules table; this
/// policy implementation is then swapped out behind the same interface.
/// </summary>
internal sealed class StaticRoutingPolicy : IRoutingPolicy
{
    private readonly IReadOnlyList<string> _order;

    public StaticRoutingPolicy(IOptions<RoutingOptions> options, ILogger<StaticRoutingPolicy> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _order = (options.Value.Providers ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .ToArray();
        logger.LogDebug("Routing policy initialised with order: [{Order}]", string.Join(",", _order));

        if (_order.Count == 0)
        {
            throw new InvalidOperationException(
                "Transaction:Routing:Providers must list at least one provider.");
        }
    }

    public IReadOnlyList<string> ResolveOrder(Guid tenantId) => _order;
}

public sealed class RoutingOptions
{
    public const string SectionName = "Transaction:Routing";

    /// <summary>
    /// Ordered provider preference for every tenant in v1. The handler walks
    /// this list, falling over on soft declines, stopping on hard declines
    /// or success.
    ///
    /// Default is empty on purpose — .NET's ConfigurationBinder appends to
    /// existing collection defaults rather than replacing them, so a non-empty
    /// default here would silently bleed into the config-bound value.
    /// Configuration is required; the StaticRoutingPolicy constructor refuses
    /// to start with an empty list.
    /// </summary>
    public string[] Providers { get; set; } = [];
}

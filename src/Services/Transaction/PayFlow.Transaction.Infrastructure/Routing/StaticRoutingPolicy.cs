using Microsoft.Extensions.Options;
using PayFlow.Transaction.Application.Abstractions;

namespace PayFlow.Transaction.Infrastructure.Routing;

/// <summary>
/// v1 — every tenant gets the same provider (configured globally via
/// <c>Transaction:Routing:DefaultProvider</c>, defaulting to "stripe").
/// Per-tenant routing rules + failover land in a later milestone when
/// the routing-rules table exists and the tenant config UI exposes it.
/// </summary>
internal sealed class StaticRoutingPolicy : IRoutingPolicy
{
    private readonly RoutingOptions _options;

    public StaticRoutingPolicy(IOptions<RoutingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public string PickProvider(Guid tenantId) => _options.DefaultProvider;
}

public sealed class RoutingOptions
{
    public const string SectionName = "Transaction:Routing";
    public string DefaultProvider { get; set; } = "stripe";
}

namespace PayFlow.Transaction.Application.Abstractions;

/// <summary>
/// Returns the ordered list of providers Transaction should try for a given
/// tenant. The handler dispatches them in order and falls over to the next
/// on a soft decline / provider-unavailable. Hard declines stop the chain.
///
/// v1 returns a static ordering from configuration; per-tenant routing
/// rules (rows in a routing_rules table) plug in here later without the
/// handler caring.
/// </summary>
public interface IRoutingPolicy
{
    IReadOnlyList<string> ResolveOrder(Guid tenantId);
}

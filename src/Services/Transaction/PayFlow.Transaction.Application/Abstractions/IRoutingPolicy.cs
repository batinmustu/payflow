namespace PayFlow.Transaction.Application.Abstractions;

/// <summary>
/// Decides which provider Transaction asks Payment to charge. v1 returns a
/// single hard-coded provider per tenant so the end-to-end flow works;
/// per-tenant routing rules + failover land in a later milestone once the
/// configuration store exists.
/// </summary>
public interface IRoutingPolicy
{
    string PickProvider(Guid tenantId);
}

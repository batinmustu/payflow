namespace PayFlow.Multitenancy;

/// <summary>
/// Default implementation of <see cref="ITenantContext"/>. Registered as a
/// scoped service so every request has its own instance — populated by the
/// middleware before any handler runs.
///
/// <see cref="Set"/> is intentionally not on the public interface; consumers
/// only read. Mutation goes through whichever code owns the lifecycle (today
/// the middleware; later, worker base classes).
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public Guid? TenantId => _tenantId;

    public bool IsResolved => _tenantId.HasValue;

    /// <summary>
    /// Set the tenant id for this scope. May be called at most once;
    /// reassigning a different tenant is a programmer mistake and throws.
    /// </summary>
    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id cannot be empty.", nameof(tenantId));
        }

        if (_tenantId.HasValue && _tenantId.Value != tenantId)
        {
            throw new InvalidOperationException(
                $"Tenant context already resolved to {_tenantId.Value}; cannot reassign to {tenantId}.");
        }

        _tenantId = tenantId;
    }
}

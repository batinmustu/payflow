namespace PayFlow.Multitenancy;

/// <summary>
/// The tenant a request is currently bound to. Set by the
/// <see cref="TenantContextMiddleware"/> from the JWT's <c>tid</c> claim
/// (or by a worker base class for background jobs); read by anything that
/// needs to scope a query, an event, or an audit log.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The current tenant id, or <c>null</c> if the request is unscoped
    /// (e.g. anonymous endpoints, infrastructure-level middleware that
    /// runs before authentication).
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// <c>true</c> when a tenant has been resolved for this scope.
    /// </summary>
    bool IsResolved { get; }
}

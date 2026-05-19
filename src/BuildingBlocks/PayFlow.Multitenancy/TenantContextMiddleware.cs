using Microsoft.AspNetCore.Http;

namespace PayFlow.Multitenancy;

/// <summary>
/// Reads the <c>tid</c> claim from the authenticated principal and assigns it
/// to the scoped <see cref="TenantContext"/>. Anonymous requests pass through
/// untouched — endpoints that need a tenant guarded by authorisation.
///
/// Must run after the JwtBearer authentication middleware so the principal is
/// populated.
/// </summary>
public sealed class TenantContextMiddleware
{
    public const string TenantClaimType = "tid";

    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var claim = context.User.FindFirst(TenantClaimType);
        if (claim is not null
            && Guid.TryParse(claim.Value, out var tenantId)
            && tenantId != Guid.Empty)
        {
            tenantContext.Set(tenantId);
        }

        await _next(context);
    }
}

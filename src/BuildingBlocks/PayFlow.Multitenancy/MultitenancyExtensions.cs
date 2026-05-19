using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace PayFlow.Multitenancy;

public static class MultitenancyExtensions
{
    /// <summary>
    /// Registers the scoped <see cref="TenantContext"/> + the
    /// <see cref="ITenantContext"/> facade. Call once per service during
    /// composition root setup.
    /// </summary>
    public static IServiceCollection AddPayFlowMultitenancy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        return services;
    }

    /// <summary>
    /// Wires the middleware that copies <c>tid</c> from the authenticated
    /// principal into <see cref="TenantContext"/>. Must be added after
    /// UseAuthentication() and before any handler that reads the tenant.
    /// </summary>
    public static IApplicationBuilder UsePayFlowMultitenancy(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<TenantContextMiddleware>();
    }
}

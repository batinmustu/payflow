using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace PayFlow.Reconciliation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowReconciliationApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace PayFlow.Payment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowPaymentApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace PayFlow.Transaction.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowTransactionApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}

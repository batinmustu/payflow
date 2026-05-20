using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Reporting.Application.Projections;

namespace PayFlow.Reporting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowReportingApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<SummaryProjectionService>();
        return services;
    }
}

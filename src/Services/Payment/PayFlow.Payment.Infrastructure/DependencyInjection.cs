using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Infrastructure.Providers;

namespace PayFlow.Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowPaymentInfrastructure(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        // Mock providers are dev-only — they return deterministic outcomes
        // based on amount mod 100 etc., which must not be wired in any
        // environment that handles real money. Production deployments must
        // register real adapters (e.g. via a different infrastructure
        // extension) before the host can build the service.
        if (environment.IsDevelopment())
        {
            services.AddSingleton<IPaymentProvider, StripeMockProvider>();
            services.AddSingleton<IPaymentProvider, IyzicoMockProvider>();
            services.AddSingleton<IPaymentProvider, PayPalMockProvider>();
        }

        services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();

        return services;
    }
}

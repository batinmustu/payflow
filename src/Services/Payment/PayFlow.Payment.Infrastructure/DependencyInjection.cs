using Microsoft.Extensions.DependencyInjection;
using PayFlow.Payment.Application.Abstractions;
using PayFlow.Payment.Infrastructure.Providers;

namespace PayFlow.Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowPaymentInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPaymentProvider, StripeMockProvider>();
        services.AddSingleton<IPaymentProvider, IyzicoMockProvider>();
        services.AddSingleton<IPaymentProvider, PayPalMockProvider>();
        services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();

        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Outbox;

namespace PayFlow.EventBus.Kafka;

public static class KafkaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="KafkaOutboxPublisher"/> as the outbox transport for
    /// the consuming service. Use as a one-line swap-in for whatever stub the
    /// service registered earlier (e.g. LoggingOutboxPublisher):
    ///
    ///     services.AddPayFlowKafkaOutboxPublisher(configuration);
    /// </summary>
    public static IServiceCollection AddPayFlowKafkaOutboxPublisher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IOutboxPublisher, KafkaOutboxPublisher>();
        return services;
    }
}

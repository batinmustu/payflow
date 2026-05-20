using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PayFlow.EventBus.Kafka.Consuming;
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

    /// <summary>
    /// Boots the consumer side: binds <see cref="KafkaConsumerOptions"/>, adds
    /// the shared <see cref="IntegrationEventRegistry"/>, and starts the
    /// background polling service. Call once per consuming service — pair with
    /// one or more <see cref="AddPayFlowKafkaConsumer{TPayload, THandler}"/>
    /// calls to bind event types to handlers.
    /// </summary>
    public static IServiceCollection AddPayFlowKafkaConsuming(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<KafkaConsumerOptions>(configuration.GetSection(KafkaConsumerOptions.SectionName));
        services.TryAddSingleton<IntegrationEventRegistry>();
        services.AddHostedService<KafkaConsumerBackgroundService>();
        return services;
    }

    /// <summary>
    /// Binds <paramref name="eventType"/> (== Kafka topic) to
    /// <typeparamref name="THandler"/>. Must be preceded by a call to
    /// <see cref="AddPayFlowKafkaConsuming"/>. The handler is resolved per
    /// message from a scoped DI scope, so EF contexts and Redis multiplexers
    /// stay scoped to one event each.
    /// </summary>
    public static IServiceCollection AddPayFlowKafkaConsumer<TPayload, THandler>(
        this IServiceCollection services,
        string eventType)
        where TPayload : class
        where THandler : class, IIntegrationEventConsumer<TPayload>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        services.TryAddSingleton<IntegrationEventRegistry>();
        services.AddScoped<THandler>();

        var registry = ResolveBootstrapRegistry(services);
        registry.Register<TPayload, THandler>(eventType);
        return services;
    }

    private static IntegrationEventRegistry ResolveBootstrapRegistry(IServiceCollection services)
    {
        // The registry has to be configured before the host builds the provider,
        // so we resolve the same singleton instance everyone else will see.
        // ServiceCollection isn't a container, so we look it up by type and
        // promote the instance to ImplementationInstance the first time.
        foreach (var d in services)
        {
            if (d.ServiceType != typeof(IntegrationEventRegistry)) continue;
            if (d.ImplementationInstance is IntegrationEventRegistry existing) return existing;
        }
        var registry = new IntegrationEventRegistry();
        services.RemoveAll<IntegrationEventRegistry>();
        services.AddSingleton(registry);
        return registry;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PayFlow.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox background service for a specific consumer
    /// <typeparamref name="TContext"/>. The consumer must add
    /// <c>OutboxMessageConfiguration</c> to that context's model and ship
    /// the matching migration.
    ///
    /// The caller separately registers an <see cref="IOutboxPublisher"/>;
    /// without one, calling this method throws at host build.
    /// </summary>
    public static IServiceCollection AddPayFlowOutbox<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxBackgroundService<TContext>>();
        return services;
    }
}

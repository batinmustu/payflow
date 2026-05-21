using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.EventBus.Kafka;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Application.Deliveries;
using PayFlow.Webhooks.Infrastructure.Http;
using PayFlow.Webhooks.Infrastructure.Persistence;
using PayFlow.Webhooks.Infrastructure.Recovery;

namespace PayFlow.Webhooks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowWebhooksInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("WebhooksDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:WebhooksDb is not configured for the Webhooks service.");

        services.AddDbContext<WebhooksDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    tableName: "__ef_migrations_history",
                    schema: WebhooksDbContext.SchemaName));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IWebhookSubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IWebhookDeliveryRepository, DeliveryRepository>();
        services.AddScoped<WebhookDispatcher>();

        services.AddSingleton<IPayloadSigner, HmacPayloadSigner>();
        services.AddHttpClient(HttpWebhookClient.ClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<IWebhookHttpClient, HttpWebhookClient>();

        services.AddHostedService<WebhookRetryService>();

        services.AddPayFlowKafkaConsuming(configuration);
        return services;
    }
}

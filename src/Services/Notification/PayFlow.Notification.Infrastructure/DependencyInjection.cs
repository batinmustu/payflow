using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PayFlow.EventBus.Kafka;
using PayFlow.Notification.Application.Abstractions;
using PayFlow.Notification.Application.Notifications;
using PayFlow.Notification.Infrastructure.Channels;
using PayFlow.Notification.Infrastructure.Messaging;
using PayFlow.Notification.Infrastructure.Persistence;
using PayFlow.Notification.Infrastructure.Templates;
using PayFlow.Notification.Infrastructure.Tenants;

namespace PayFlow.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:NotificationDb is not configured for the Notification service.");

        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    tableName: "__ef_migrations_history",
                    schema: NotificationDbContext.SchemaName));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<NotificationDispatcher>();

        services.AddSingleton<ITemplateRenderer, PlaceholderTemplateRenderer>();
        services.AddSingleton<ITenantContactResolver, DeterministicTenantContactResolver>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddSingleton<ISmsSender, LoggingSmsSender>();

        AddRetryQueue(services, configuration);

        // Consume-only. Topic bindings live in the API layer via
        // AddPayFlowKafkaConsumer<,>().
        services.AddPayFlowKafkaConsuming(configuration);

        return services;
    }

    private static void AddRetryQueue(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        var rabbitConnectionString = configuration[$"{RabbitMqOptions.SectionName}:ConnectionString"];
        if (string.IsNullOrWhiteSpace(rabbitConnectionString))
        {
            // No broker configured → behavior is "transient failures stay
            // Pending, no retry consumer runs". Same shape as before this
            // feature landed, but the dispatcher now surfaces a warning log
            // so operators see the missing config in production.
            services.AddSingleton<INotificationRetryQueue, NullNotificationRetryQueue>();
            return;
        }

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<INotificationRetryQueue, RabbitMqRetryQueue>();
        services.AddHostedService<NotificationRetryConsumer>();
    }
}

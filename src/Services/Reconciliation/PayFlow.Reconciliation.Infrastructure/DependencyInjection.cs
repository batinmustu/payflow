using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.EventBus.Kafka;
using PayFlow.Outbox;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Infrastructure.Persistence;

namespace PayFlow.Reconciliation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowReconciliationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("ReconciliationDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:ReconciliationDb is not configured for the Reconciliation service.");

        services.AddDbContext<ReconciliationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    tableName: "__ef_migrations_history",
                    schema: ReconciliationDbContext.SchemaName));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Outbox publishing + consumer side both ride on the Kafka building
        // block. The saga (M4.D) will use AddPayFlowKafkaConsumer<TPayload,
        // THandler>() to bind RefundRequested → its handler.
        services.AddPayFlowKafkaOutboxPublisher(configuration);
        services.AddPayFlowOutbox<ReconciliationDbContext>(configuration);
        services.AddPayFlowKafkaConsuming(configuration);

        return services;
    }
}

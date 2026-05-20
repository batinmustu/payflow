using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.EventBus.Kafka;
using PayFlow.Reporting.Application.Abstractions;
using PayFlow.Reporting.Infrastructure.Maintenance;
using PayFlow.Reporting.Infrastructure.Persistence;

namespace PayFlow.Reporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayFlowReportingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("ReportingDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:ReportingDb is not configured for the Reporting service.");

        services.AddDbContext<ReportingDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    tableName: "__ef_migrations_history",
                    schema: ReportingDbContext.SchemaName));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISummaryRepository, SummaryRepository>();
        services.AddScoped<IProcessedEventStore, ProcessedEventStore>();

        // Reporting is consume-only — no producer wiring. The actual topic
        // bindings (4 event types) live in the API layer.
        services.AddPayFlowKafkaConsuming(configuration);

        // Trim the idempotency table on a 6-hour cadence; without this it
        // grows linearly with consumed messages.
        services.AddHostedService<ProcessedEventsRetentionService>();

        return services;
    }
}

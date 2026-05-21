using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayFlow.EventBus.Kafka;
using PayFlow.Multitenancy;
using PayFlow.Outbox;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Application.RefundSagas;
using PayFlow.Reconciliation.Infrastructure.Payments;
using PayFlow.Reconciliation.Infrastructure.Persistence;
using PayFlow.Reconciliation.Infrastructure.RefundSagas;

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

        services.AddSingleton<DomainEventToOutboxInterceptor>();

        services.AddDbContext<ReconciliationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    tableName: "__ef_migrations_history",
                    schema: ReconciliationDbContext.SchemaName));
            options.AddInterceptors(sp.GetRequiredService<DomainEventToOutboxInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRefundSagaRepository, RefundSagaRepository>();
        services.AddScoped<RefundSagaProcessor>();

        services.Configure<PaymentServiceOptions>(configuration.GetSection(PaymentServiceOptions.SectionName));
        services.AddSingleton<IServiceTokenIssuer, ServiceTokenIssuer>();
        services.AddTransient<RetryingHttpMessageHandler>();
        services.AddHttpClient<IPaymentRefundClient, HttpPaymentRefundClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<PaymentServiceOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        })
        .AddHttpMessageHandler<RetryingHttpMessageHandler>();

        // Outbox publishing + consumer side both ride on the Kafka building
        // block. The actual RefundRequested → consumer binding is registered
        // by the API layer via AddPayFlowKafkaConsumer<,>().
        services.AddPayFlowKafkaOutboxPublisher(configuration);
        services.AddPayFlowOutbox<ReconciliationDbContext>(configuration);
        services.AddPayFlowKafkaConsuming(configuration);

        // Periodic recovery for sagas stuck in ProviderCalled — drains the
        // retry budget when a transient failure leaves the saga mid-flight.
        services.AddHostedService<RefundSagaRecoveryService>();

        return services;
    }
}

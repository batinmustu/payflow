using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Outbox;
using PayFlow.Reconciliation.Application;
using PayFlow.Reconciliation.Application.Abstractions;
using PayFlow.Reconciliation.Application.RefundSagas;
using PayFlow.Reconciliation.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PayFlow.Reconciliation.IntegrationTests;

/// <summary>
/// Boots a real Postgres in a container, applies the Reconciliation migrations,
/// and exposes a DI scope that wires the saga consumer + processor against an
/// in-memory Payment refund client so tests can drive the choreography without
/// any HTTP or Kafka.
/// </summary>
public sealed class ReconciliationDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("payflow_test")
        .WithUsername("payflow")
        .WithPassword("payflow")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;
    public InMemoryPaymentRefundClient PaymentMock { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        // Same outbox interceptor the real Infrastructure DI registers — saga
        // domain events become outbox_messages rows in the same SaveChanges.
        services.AddSingleton<DomainEventToOutboxInterceptor>();
        services.AddDbContext<ReconciliationDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(_postgres.GetConnectionString(), pg =>
                pg.MigrationsHistoryTable("__ef_migrations_history", ReconciliationDbContext.SchemaName));
            opts.AddInterceptors(sp.GetRequiredService<DomainEventToOutboxInterceptor>());
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRefundSagaRepository, RefundSagaRepository>();
        services.AddScoped<RefundSagaProcessor>();
        services.AddSingleton<IPaymentRefundClient>(PaymentMock);
        services.AddPayFlowReconciliationApplication();

        Services = services.BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

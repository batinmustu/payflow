using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Webhooks.Application;
using PayFlow.Webhooks.Application.Abstractions;
using PayFlow.Webhooks.Application.Deliveries;
using PayFlow.Webhooks.Infrastructure.Http;
using PayFlow.Webhooks.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PayFlow.Webhooks.IntegrationTests;

/// <summary>
/// Spins up a real Postgres in a Testcontainers container, applies the
/// Webhooks migrations, and wires the dispatcher + repositories + the
/// production HMAC signer against a recording HTTP client double. Tests
/// then exercise the full Kafka-event-to-delivery-row pipeline without
/// any actual network hops.
/// </summary>
public sealed class WebhooksDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("payflow_test")
        .WithUsername("payflow")
        .WithPassword("payflow")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;
    public RecordingWebhookHttpClient HttpMock { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<WebhooksDbContext>(opts =>
            opts.UseNpgsql(_postgres.GetConnectionString(), pg =>
                pg.MigrationsHistoryTable("__ef_migrations_history", WebhooksDbContext.SchemaName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IWebhookSubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IWebhookDeliveryRepository, DeliveryRepository>();
        services.AddScoped<WebhookDispatcher>();

        // Real signer + recording transport — the dispatcher signs with the
        // production code path; tests can assert against the exact bytes.
        services.AddSingleton<IPayloadSigner, HmacPayloadSigner>();
        services.AddSingleton<IWebhookHttpClient>(HttpMock);

        services.AddPayFlowWebhooksApplication();

        Services = services.BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebhooksDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

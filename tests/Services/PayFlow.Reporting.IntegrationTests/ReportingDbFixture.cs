using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayFlow.Reporting.Application;
using PayFlow.Reporting.Application.Abstractions;
using PayFlow.Reporting.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PayFlow.Reporting.IntegrationTests;

/// <summary>
/// Boots a real Postgres in a container, applies the Reporting migrations,
/// and exposes a tiny DI scope for the projection service so tests can hit
/// the real EF + Postgres path (concurrency races, ExecuteDeleteAsync, etc.)
/// without spinning up the API host.
/// </summary>
public sealed class ReportingDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("payflow_test")
        .WithUsername("payflow")
        .WithPassword("payflow")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ReportingDbContext>(opts =>
        {
            opts.UseNpgsql(_postgres.GetConnectionString(), pg =>
                pg.MigrationsHistoryTable("__ef_migrations_history", ReportingDbContext.SchemaName));
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISummaryRepository, SummaryRepository>();
        services.AddScoped<IProcessedEventStore, ProcessedEventStore>();
        services.AddPayFlowReportingApplication();

        Services = services.BuildServiceProvider();

        // Apply migrations against the fresh container.
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

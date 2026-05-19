using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace PayFlow.Identity.IntegrationTests;

/// <summary>
/// Spins up a real Postgres in a container and points an in-process Identity
/// host at it. Migrations run on startup (the API does this in Development
/// mode), so the schema is ready before any test runs.
/// </summary>
public sealed class IdentityAppFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("payflow_test")
        .WithUsername("payflow")
        .WithPassword("payflow")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = ConnectionString,
                ["Identity:Jwt:Issuer"] = "PayFlow.Identity.Test",
                ["Identity:Jwt:Audience"] = "PayFlow.Services.Test",
                ["Identity:Jwt:SigningKey"] = "integration-test-signing-key-min-32-bytes",
                ["Identity:Jwt:AccessTokenLifetimeMinutes"] = "15",
                // Silence Seq sink — no Seq running in the test process.
                ["Observability:Seq:ServerUrl"] = "http://localhost:65535",
            });
        });
    }
}

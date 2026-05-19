using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayFlow.Identity.Infrastructure.Persistence;

/// <summary>
/// Used by the `dotnet ef` tooling at design time only — never at runtime.
/// Lets `dotnet ef migrations add` operate on the Infrastructure project
/// directly, without needing the API host to be runnable.
/// </summary>
internal sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=payflow;Username=payflow;Password=payflow",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.SchemaName))
            .Options;

        return new IdentityDbContext(options);
    }
}

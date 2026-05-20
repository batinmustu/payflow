using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PayFlow.Transaction.Infrastructure.Persistence;

internal sealed class TransactionDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TransactionDbContext>
{
    public TransactionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=payflow;Username=payflow;Password=payflow",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TransactionDbContext.SchemaName))
            .Options;

        return new TransactionDbContext(options);
    }
}

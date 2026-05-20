using Microsoft.EntityFrameworkCore;
using PayFlow.Outbox;

namespace PayFlow.Reconciliation.Infrastructure.Persistence;

public sealed class ReconciliationDbContext : DbContext
{
    public const string SchemaName = "reconciliation";

    public ReconciliationDbContext(DbContextOptions<ReconciliationDbContext> options) : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReconciliationDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}

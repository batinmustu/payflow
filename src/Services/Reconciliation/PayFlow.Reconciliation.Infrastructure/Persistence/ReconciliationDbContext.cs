using Microsoft.EntityFrameworkCore;
using PayFlow.Outbox;
using PayFlow.Reconciliation.Domain.RefundSagas;

namespace PayFlow.Reconciliation.Infrastructure.Persistence;

public sealed class ReconciliationDbContext : DbContext
{
    public const string SchemaName = "reconciliation";

    public ReconciliationDbContext(DbContextOptions<ReconciliationDbContext> options) : base(options)
    {
    }

    public DbSet<RefundSaga> RefundSagas => Set<RefundSaga>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReconciliationDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}

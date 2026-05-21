using Microsoft.EntityFrameworkCore;
using PayFlow.Webhooks.Domain.Deliveries;
using PayFlow.Webhooks.Domain.Subscriptions;

namespace PayFlow.Webhooks.Infrastructure.Persistence;

public sealed class WebhooksDbContext : DbContext
{
    public const string SchemaName = "webhooks";

    public WebhooksDbContext(DbContextOptions<WebhooksDbContext> options) : base(options) { }

    public DbSet<WebhookSubscription> Subscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> Deliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WebhooksDbContext).Assembly);
    }
}

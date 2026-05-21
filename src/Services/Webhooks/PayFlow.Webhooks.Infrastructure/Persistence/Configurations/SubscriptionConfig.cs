using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Webhooks.Domain.Subscriptions;

namespace PayFlow.Webhooks.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionConfig : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> b)
    {
        b.ToTable("subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        b.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(200);
        b.Property(x => x.Url).HasColumnName("url").IsRequired().HasMaxLength(2048);
        b.Property(x => x.Secret).HasColumnName("secret").IsRequired().HasMaxLength(128);
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.DeactivatedAt).HasColumnName("deactivated_at");
        b.HasIndex(x => new { x.TenantId, x.EventType, x.IsActive })
            .HasDatabaseName("ix_subscriptions_tenant_event_active");
        b.Ignore(x => x.DomainEvents);
    }
}

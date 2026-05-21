using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Webhooks.Domain.Deliveries;

namespace PayFlow.Webhooks.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryConfig : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.ToTable("deliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        b.Property(x => x.SubscriptionId).HasColumnName("subscription_id").IsRequired();
        b.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(200);
        b.Property(x => x.SourceMessageId).HasColumnName("source_message_id").IsRequired();
        b.Property(x => x.TargetUrl).HasColumnName("target_url").IsRequired().HasMaxLength(2048);
        b.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Signature).HasColumnName("signature").IsRequired().HasMaxLength(200);
        b.Property(x => x.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        b.Property(x => x.LastStatusCode).HasColumnName("last_status_code");
        b.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.FailedAt).HasColumnName("failed_at");

        b.HasIndex(x => new { x.SubscriptionId, x.SourceMessageId })
            .IsUnique()
            .HasDatabaseName("ux_deliveries_subscription_source");
        b.HasIndex(x => new { x.State, x.NextAttemptAt })
            .HasDatabaseName("ix_deliveries_state_due");
        b.HasIndex(x => new { x.TenantId, x.CreatedAt })
            .HasDatabaseName("ix_deliveries_tenant_created");
        b.Ignore(x => x.DomainEvents);
    }
}

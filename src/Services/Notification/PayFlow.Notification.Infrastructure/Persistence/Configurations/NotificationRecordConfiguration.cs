using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayFlow.Notification.Domain.Notifications;

namespace PayFlow.Notification.Infrastructure.Persistence.Configurations;

internal sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(n => n.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(n => n.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(n => n.State)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(n => n.Recipient).HasColumnName("recipient").HasMaxLength(256).IsRequired();
        builder.Property(n => n.Subject).HasColumnName("subject").HasMaxLength(256).IsRequired();
        builder.Property(n => n.Body).HasColumnName("body").IsRequired();

        builder.Property(n => n.SourceMessageId).HasColumnName("source_message_id").IsRequired();
        builder.Property(n => n.SourceEventType).HasColumnName("source_event_type").HasMaxLength(128).IsRequired();

        builder.Property(n => n.FailureReason).HasColumnName("failure_reason").HasMaxLength(128);
        builder.Property(n => n.ProviderReference).HasColumnName("provider_reference").HasMaxLength(128);
        builder.Property(n => n.AttemptCount).HasColumnName("attempt_count").IsRequired();

        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(n => n.SentAt).HasColumnName("sent_at");
        builder.Property(n => n.FailedAt).HasColumnName("failed_at");

        // Dedup key: one notification per source Kafka message.
        builder.HasIndex(n => n.SourceMessageId)
            .IsUnique()
            .HasDatabaseName("ux_notifications_source_message");

        builder.HasIndex(n => new { n.TenantId, n.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_notifications_tenant_created_desc");

        builder.Ignore(n => n.DomainEvents);
    }
}

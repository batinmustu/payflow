using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PayFlow.Outbox;

/// <summary>
/// Standard EF mapping for <see cref="OutboxMessage"/>. A consuming service
/// applies it in its own DbContext's OnModelCreating:
///
///     modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
///
/// The table lands in the consumer's schema (alongside its business tables)
/// so the outbox write and the state-change write are in the same DB
/// transaction by construction.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.AggregateType).HasColumnName("aggregate_type").HasMaxLength(64).IsRequired();
        builder.Property(m => m.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.Headers).HasColumnName("headers").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(m => m.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(1024);
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(m => m.PublishedAt).HasColumnName("published_at");

        // Hot index: the publisher's "give me work" query
        // (state in [Pending, Failed] AND next_attempt_at <= now).
        builder.HasIndex(m => new { m.State, m.NextAttemptAt })
            .HasDatabaseName("ix_outbox_messages_state_next_attempt");

        // Retention sweep walks by published_at.
        builder.HasIndex(m => m.PublishedAt)
            .HasDatabaseName("ix_outbox_messages_published_at");
    }
}

using Messaging.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.Domain.Shared.Configurations;

public sealed class MessageOutboxEventConfiguration : IEntityTypeConfiguration<MessageOutboxEvent>
{
    public void Configure(EntityTypeBuilder<MessageOutboxEvent> entity)
    {
        entity.HasKey(e => e.Id).HasName("messageoutboxevent_pk");

        entity.ToTable("MessageOutboxEvent", "Messaging");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.EventType)
            .HasColumnType("character varying")
            .HasMaxLength(128);
        entity.Property(e => e.AggregateType)
            .HasColumnType("character varying")
            .HasMaxLength(128);
        entity.Property(e => e.PayloadJson).HasColumnType("jsonb");
        entity.Property(e => e.LastError).HasColumnType("character varying");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");

        entity.HasIndex(e => new { e.TenantId, e.ProcessedAt, e.OccurredAt })
            .HasDatabaseName("IX_MessageOutboxEvent_Tenant_Processed_Occurred");

        entity.HasIndex(e => new { e.ThreadId, e.OccurredAt })
            .HasDatabaseName("IX_MessageOutboxEvent_Thread_Occurred");

        entity.HasIndex(e => new { e.EventType, e.OccurredAt })
            .HasDatabaseName("IX_MessageOutboxEvent_EventType_Occurred");
    }
}

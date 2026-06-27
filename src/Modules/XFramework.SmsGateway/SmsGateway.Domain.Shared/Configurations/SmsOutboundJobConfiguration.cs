using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmsGateway.Domain.Shared.Contracts;

namespace SmsGateway.Domain.Shared.Configurations;

public sealed class SmsOutboundJobConfiguration : IEntityTypeConfiguration<SmsOutboundJob>
{
    public void Configure(EntityTypeBuilder<SmsOutboundJob> entity)
    {
        entity.HasKey(e => e.Id).HasName("smsoutboundjob_pk");
        entity.ToTable("SmsOutboundJob", "SmsGateway");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.Sender).HasMaxLength(128);
        entity.Property(e => e.Recipient).IsRequired().HasMaxLength(128);
        entity.Property(e => e.Subject).HasMaxLength(256);
        entity.Property(e => e.Intent).HasMaxLength(128);
        entity.Property(e => e.Message).IsRequired().HasMaxLength(4000);
        entity.Property(e => e.LeaseOwner).HasMaxLength(128);
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.Property(e => e.ProviderMessageId).HasMaxLength(256);
        entity.Property(e => e.LastErrorCode).HasMaxLength(128);
        entity.Property(e => e.LastErrorMessage).HasMaxLength(2000);

        entity.HasIndex(e => new { e.TenantId, e.AgentClusterId, e.Status, e.NextAttemptAt })
            .HasDatabaseName("ix_smsoutboundjob_tenant_cluster_status_nextattempt");
        entity.HasIndex(e => new { e.TenantId, e.CorrelationId })
            .IsUnique()
            .HasFilter("\"CorrelationId\" IS NOT NULL")
            .HasDatabaseName("ux_smsoutboundjob_tenant_correlation");
        entity.HasIndex(e => new { e.TenantId, e.NotificationDeliveryJobId })
            .HasDatabaseName("ix_smsoutboundjob_tenant_notification_delivery_job");
    }
}

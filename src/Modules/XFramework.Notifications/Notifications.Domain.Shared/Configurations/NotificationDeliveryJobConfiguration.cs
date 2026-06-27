using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Shared.Contracts;

namespace Notifications.Domain.Shared.Configurations;

public sealed class NotificationDeliveryJobConfiguration : IEntityTypeConfiguration<NotificationDeliveryJob>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryJob> entity)
    {
        entity.HasKey(e => e.Id).HasName("notificationdeliveryjob_pk");
        entity.ToTable("NotificationDeliveryJob", "Notifications");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.Channel).HasConversion<int>();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.ProviderKey).HasMaxLength(128);
        entity.Property(e => e.RecipientAddress).HasMaxLength(512);
        entity.Property(e => e.PayloadJson).HasColumnType("text");
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.Property(e => e.LeaseOwner).HasMaxLength(128);
        entity.Property(e => e.ProviderMessageId).HasMaxLength(256);
        entity.Property(e => e.LastErrorCode).HasMaxLength(128);
        entity.Property(e => e.LastErrorMessage).HasMaxLength(2000);

        entity.HasIndex(e => new { e.TenantId, e.Status, e.NextAttemptAt })
            .HasDatabaseName("ix_notificationdeliveryjob_tenant_status_nextattempt");
        entity.HasIndex(e => new { e.TenantId, e.NotificationInboxItemId, e.Channel })
            .IsUnique()
            .HasDatabaseName("ux_notificationdeliveryjob_tenant_item_channel");
        entity.HasIndex(e => new { e.TenantId, e.CorrelationId })
            .IsUnique()
            .HasFilter("\"CorrelationId\" IS NOT NULL")
            .HasDatabaseName("ux_notificationdeliveryjob_tenant_correlation");

        entity.HasOne(e => e.NotificationInboxItem)
            .WithMany()
            .HasForeignKey(e => e.NotificationInboxItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("notificationdeliveryjob_inboxitem_id_fk");
    }
}

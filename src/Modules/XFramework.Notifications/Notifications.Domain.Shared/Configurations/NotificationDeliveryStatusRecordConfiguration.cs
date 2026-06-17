using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Shared.Contracts;

namespace Notifications.Domain.Shared.Configurations;

public sealed class NotificationDeliveryStatusRecordConfiguration :
    IEntityTypeConfiguration<NotificationDeliveryStatusRecord>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryStatusRecord> entity)
    {
        entity.HasKey(e => e.Id).HasName("notificationdeliverystatus_pk");
        entity.ToTable("NotificationDeliveryStatus", "Notifications");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.Channel).HasConversion<int>();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.ProviderMessageId).HasMaxLength(256);
        entity.Property(e => e.ErrorCode).HasMaxLength(128);
        entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        entity.Property(e => e.RecordedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.TenantId, e.NotificationInboxItemId, e.Channel })
            .IsUnique()
            .HasDatabaseName("ux_notificationdeliverystatus_tenant_item_channel");

        entity.HasOne(e => e.NotificationInboxItem)
            .WithMany(e => e.DeliveryStatuses)
            .HasForeignKey(e => e.NotificationInboxItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("notificationdeliverystatus_inboxitem_id_fk");
    }
}

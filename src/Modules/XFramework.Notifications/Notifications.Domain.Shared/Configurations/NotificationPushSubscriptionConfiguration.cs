using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Shared.Contracts;

namespace Notifications.Domain.Shared.Configurations;

public sealed class NotificationPushSubscriptionConfiguration : IEntityTypeConfiguration<NotificationPushSubscription>
{
    public void Configure(EntityTypeBuilder<NotificationPushSubscription> entity)
    {
        entity.HasKey(e => e.Id).HasName("notificationpushsubscription_pk");
        entity.ToTable("NotificationPushSubscription", "Notifications");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");

        // FCM and Mozilla endpoints run long; the hash carries uniqueness so the btree stays small.
        entity.Property(e => e.Endpoint).IsRequired().HasColumnType("text");
        entity.Property(e => e.EndpointHash).IsRequired().HasMaxLength(64).IsFixedLength();
        entity.Property(e => e.P256dh).IsRequired().HasMaxLength(128);
        entity.Property(e => e.Auth).IsRequired().HasMaxLength(64);
        entity.Property(e => e.DeviceLabel).HasMaxLength(64);
        entity.Property(e => e.LastErrorCode).HasMaxLength(64);

        // One row per endpoint per tenant: a re-subscribing device updates in place rather than
        // accumulating duplicates that would each deliver the same notification.
        entity.HasIndex(e => new { e.TenantId, e.EndpointHash })
            .IsUnique()
            .HasDatabaseName("ux_notificationpushsubscription_tenant_endpoint");
        entity.HasIndex(e => new { e.TenantId, e.CredentialId })
            .HasDatabaseName("ix_notificationpushsubscription_tenant_credential");
    }
}

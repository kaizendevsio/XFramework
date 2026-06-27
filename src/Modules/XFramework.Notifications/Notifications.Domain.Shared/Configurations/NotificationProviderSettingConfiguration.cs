using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Shared.Contracts;

namespace Notifications.Domain.Shared.Configurations;

public sealed class NotificationProviderSettingConfiguration : IEntityTypeConfiguration<NotificationProviderSetting>
{
    public void Configure(EntityTypeBuilder<NotificationProviderSetting> entity)
    {
        entity.HasKey(e => e.Id).HasName("notificationprovidersetting_pk");
        entity.ToTable("NotificationProviderSetting", "Notifications");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.Channel).HasConversion<int>();
        entity.Property(e => e.ProviderKey).IsRequired().HasMaxLength(128);
        entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
        entity.Property(e => e.SettingsJson).HasColumnType("text");
        entity.Property(e => e.LastHealthStatus).HasMaxLength(128);

        entity.HasIndex(e => new { e.TenantId, e.Channel, e.ProviderKey })
            .IsUnique()
            .HasDatabaseName("ux_notificationprovidersetting_tenant_channel_key");
    }
}

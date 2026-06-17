using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Shared.Contracts;

namespace Notifications.Domain.Shared.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> entity)
    {
        entity.HasKey(e => e.Id).HasName("notificationpreference_pk");
        entity.ToTable("NotificationPreference", "Notifications");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.EnabledChannels).HasConversion<int>();
        entity.Property(e => e.DisabledTemplateKeys).HasColumnType("text");

        entity.HasIndex(e => new { e.TenantId, e.CredentialId })
            .IsUnique()
            .HasDatabaseName("ux_notificationpreference_tenant_credential");
    }
}

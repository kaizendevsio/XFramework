using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Shared.Contracts;

namespace Notifications.Domain.Shared.Configurations;

public sealed class NotificationInboxItemConfiguration : IEntityTypeConfiguration<NotificationInboxItem>
{
    public void Configure(EntityTypeBuilder<NotificationInboxItem> entity)
    {
        entity.HasKey(e => e.Id).HasName("notificationinboxitem_pk");
        entity.ToTable("NotificationInboxItem", "Notifications");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.TemplateKey).HasMaxLength(128);
        entity.Property(e => e.Title).HasMaxLength(256);
        entity.Property(e => e.Body).HasMaxLength(4000);
        entity.Property(e => e.CorrelationId).HasMaxLength(128);
        entity.Property(e => e.DataJson).HasColumnType("text");
        entity.Property(e => e.DeliveryChannels).HasConversion<int>();

        entity.HasIndex(e => new { e.TenantId, e.RecipientCredentialId, e.IsRead, e.CreatedAt })
            .HasDatabaseName("ix_notificationinboxitem_tenant_recipient_read_created");
        entity.HasIndex(e => new { e.TenantId, e.CorrelationId })
            .HasDatabaseName("ix_notificationinboxitem_tenant_correlation");
        entity.HasIndex(e => new { e.TenantId, e.TemplateKey })
            .HasDatabaseName("ix_notificationinboxitem_tenant_template");
    }
}

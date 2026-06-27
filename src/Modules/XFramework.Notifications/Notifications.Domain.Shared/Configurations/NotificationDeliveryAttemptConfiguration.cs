using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Shared.Contracts;

namespace Notifications.Domain.Shared.Configurations;

public sealed class NotificationDeliveryAttemptConfiguration : IEntityTypeConfiguration<NotificationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> entity)
    {
        entity.HasKey(e => e.Id).HasName("notificationdeliveryattempt_pk");
        entity.ToTable("NotificationDeliveryAttempt", "Notifications");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValueSql("true");
        entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValueSql("false");
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.ProviderKey).HasMaxLength(128);
        entity.Property(e => e.ProviderMessageId).HasMaxLength(256);
        entity.Property(e => e.ErrorCode).HasMaxLength(128);
        entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");

        entity.HasIndex(e => new { e.TenantId, e.NotificationDeliveryJobId, e.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("ux_notificationdeliveryattempt_tenant_job_attempt");

        entity.HasOne(e => e.NotificationDeliveryJob)
            .WithMany(e => e.Attempts)
            .HasForeignKey(e => e.NotificationDeliveryJobId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("notificationdeliveryattempt_job_id_fk");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XFramework.Domain.Shared.Contracts.Base;

namespace Attendance.Domain.Shared.Configurations;

internal static class AttendanceConfigurationExtensions
{
    public static void ConfigureAttendanceBaseModel<TEntity>(
        this EntityTypeBuilder<TEntity> entity,
        string primaryKeyName)
        where TEntity : BaseModel
    {
        entity.HasKey(e => e.Id).HasName(primaryKeyName);

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");

        entity.Property(e => e.TenantId).IsRequired();
        entity.Property(e => e.IsEnabled).HasDefaultValue(true);
        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ConcurrencyStamp)
            .IsConcurrencyToken()
            .HasDefaultValueSql("(uuid_generate_v4())");

        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => new { e.TenantId, e.IsDeleted });
    }
}

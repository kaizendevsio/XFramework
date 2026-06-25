using Attendance.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Domain.Shared.Configurations;

public sealed class AttendancePolicyConfiguration : IEntityTypeConfiguration<AttendancePolicy>
{
    public void Configure(EntityTypeBuilder<AttendancePolicy> entity)
    {
        entity.ToTable("AttendancePolicy", "Attendance");
        entity.ConfigureAttendanceBaseModel("PK_Attendance_Policy");

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.Property(e => e.TimeZoneId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.GracePeriodMinutes).HasDefaultValue(5);
        entity.Property(e => e.EarlyCheckoutGraceMinutes).HasDefaultValue(0);
        entity.Property(e => e.CheckoutRequired).HasDefaultValue(true);

        entity.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_AttendancePolicy_Tenant_Name_Active");
    }
}


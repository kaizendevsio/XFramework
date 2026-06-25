using Attendance.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Domain.Shared.Configurations;

public sealed class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
{
    public void Configure(EntityTypeBuilder<AttendanceSession> entity)
    {
        entity.ToTable("AttendanceSession", "Attendance");
        entity.ConfigureAttendanceBaseModel("PK_Attendance_Session");

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Code).HasMaxLength(64);
        entity.Property(e => e.TimeZoneId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Status).HasConversion<int>();

        entity.HasIndex(e => new { e.TenantId, e.ContextId, e.StartsAt })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Context_Start");
        entity.HasIndex(e => new { e.TenantId, e.ContextId, e.Code })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"Code\" IS NOT NULL AND \"Code\" <> ''")
            .HasDatabaseName("UX_AttendanceSession_Tenant_Context_Code_Active");

        entity.HasOne(e => e.Context)
            .WithMany(e => e.Sessions)
            .HasForeignKey(e => e.ContextId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Session_Context");

        entity.HasOne(e => e.Policy)
            .WithMany(e => e.Sessions)
            .HasForeignKey(e => e.PolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Session_Policy");
    }
}


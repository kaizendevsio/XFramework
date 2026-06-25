using Attendance.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Domain.Shared.Configurations;

public sealed class AttendanceContextConfiguration : IEntityTypeConfiguration<AttendanceContext>
{
    public void Configure(EntityTypeBuilder<AttendanceContext> entity)
    {
        entity.ToTable("AttendanceContext", "Attendance");
        entity.ConfigureAttendanceBaseModel("PK_Attendance_Context");

        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Code).HasMaxLength(64);
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.Property(e => e.ContextType).HasConversion<int>();
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasIndex(e => new { e.TenantId, e.ContextType, e.IsActive })
            .HasDatabaseName("IX_AttendanceContext_Tenant_Type_Active");
        entity.HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"Code\" IS NOT NULL AND \"Code\" <> ''")
            .HasDatabaseName("UX_AttendanceContext_Tenant_Code_Active");

        entity.HasOne(e => e.DefaultPolicy)
            .WithMany(e => e.Contexts)
            .HasForeignKey(e => e.DefaultPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Context_DefaultPolicy");
    }
}


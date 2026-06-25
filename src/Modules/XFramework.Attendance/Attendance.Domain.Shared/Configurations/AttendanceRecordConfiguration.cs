using Attendance.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Domain.Shared.Configurations;

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> entity)
    {
        entity.ToTable("AttendanceRecord", "Attendance");
        entity.ConfigureAttendanceBaseModel("PK_Attendance_Record");

        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.Notes).HasMaxLength(2000);

        entity.HasIndex(e => new { e.TenantId, e.SessionId, e.ParticipantId })
            .IsUnique()
            .HasDatabaseName("UX_AttendanceRecord_Tenant_Session_Participant");
        entity.HasIndex(e => new { e.TenantId, e.CredentialId, e.SessionId })
            .HasDatabaseName("IX_AttendanceRecord_Tenant_Credential_Session");
        entity.HasIndex(e => new { e.TenantId, e.Status, e.SessionId })
            .HasDatabaseName("IX_AttendanceRecord_Tenant_Status_Session");

        entity.HasOne(e => e.Session)
            .WithMany(e => e.Records)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Record_Session");

        entity.HasOne(e => e.Participant)
            .WithMany(e => e.Records)
            .HasForeignKey(e => e.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Record_Participant");
    }
}


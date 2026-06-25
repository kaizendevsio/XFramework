using Attendance.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Domain.Shared.Configurations;

public sealed class AttendanceAdjustmentConfiguration : IEntityTypeConfiguration<AttendanceAdjustment>
{
    public void Configure(EntityTypeBuilder<AttendanceAdjustment> entity)
    {
        entity.ToTable("AttendanceAdjustment", "Attendance");
        entity.ConfigureAttendanceBaseModel("PK_Attendance_Adjustment");

        entity.Property(e => e.PreviousStatus).HasConversion<int>();
        entity.Property(e => e.NewStatus).HasConversion<int>();
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(2000);

        entity.HasIndex(e => new { e.TenantId, e.SessionId, e.ParticipantId, e.CreatedAt })
            .HasDatabaseName("IX_AttendanceAdjustment_Tenant_Session_Participant_Created");
        entity.HasIndex(e => new { e.TenantId, e.ActorCredentialId, e.CreatedAt })
            .HasDatabaseName("IX_AttendanceAdjustment_Tenant_Actor_Created");

        entity.HasOne(e => e.Record)
            .WithMany(e => e.Adjustments)
            .HasForeignKey(e => e.RecordId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Adjustment_Record");
    }
}


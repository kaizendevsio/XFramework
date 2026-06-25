using Attendance.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Domain.Shared.Configurations;

public sealed class AttendanceEventConfiguration : IEntityTypeConfiguration<AttendanceEvent>
{
    public void Configure(EntityTypeBuilder<AttendanceEvent> entity)
    {
        entity.ToTable("AttendanceEvent", "Attendance");
        entity.ConfigureAttendanceBaseModel("PK_Attendance_Event");

        entity.Property(e => e.EventType).HasConversion<int>();
        entity.Property(e => e.Source).HasConversion<int>();
        entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(128);
        entity.Property(e => e.SourceReference).HasMaxLength(256);
        entity.Property(e => e.Notes).HasMaxLength(2000);
        entity.Property(e => e.MetadataJson).HasColumnType("jsonb");

        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_AttendanceEvent_Tenant_IdempotencyKey");
        entity.HasIndex(e => new { e.TenantId, e.SessionId, e.ParticipantId, e.OccurredAt })
            .HasDatabaseName("IX_AttendanceEvent_Tenant_Session_Participant_Occurred");
        entity.HasIndex(e => new { e.TenantId, e.CredentialId, e.OccurredAt })
            .HasDatabaseName("IX_AttendanceEvent_Tenant_Credential_Occurred");

        entity.HasOne(e => e.Session)
            .WithMany(e => e.Events)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Event_Session");

        entity.HasOne(e => e.Participant)
            .WithMany()
            .HasForeignKey(e => e.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Event_Participant");
    }
}


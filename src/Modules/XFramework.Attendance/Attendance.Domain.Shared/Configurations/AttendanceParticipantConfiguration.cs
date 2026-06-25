using Attendance.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Domain.Shared.Configurations;

public sealed class AttendanceParticipantConfiguration : IEntityTypeConfiguration<AttendanceParticipant>
{
    public void Configure(EntityTypeBuilder<AttendanceParticipant> entity)
    {
        entity.ToTable("AttendanceParticipant", "Attendance");
        entity.ConfigureAttendanceBaseModel("PK_Attendance_Participant");

        entity.Property(e => e.DisplayName).HasMaxLength(200);
        entity.Property(e => e.ReferenceCode).HasMaxLength(128);
        entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsActive).HasDefaultValue(true);

        entity.HasIndex(e => new { e.TenantId, e.ContextId, e.CredentialId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_AttendanceParticipant_Tenant_Context_Credential_Active");
        entity.HasIndex(e => new { e.TenantId, e.CredentialId, e.IsActive })
            .HasDatabaseName("IX_AttendanceParticipant_Tenant_Credential_Active");
        entity.HasIndex(e => new { e.TenantId, e.ContextId, e.ReferenceCode })
            .HasFilter("\"ReferenceCode\" IS NOT NULL AND \"ReferenceCode\" <> ''")
            .HasDatabaseName("IX_AttendanceParticipant_Tenant_Context_Reference");

        entity.HasOne(e => e.Context)
            .WithMany(e => e.Participants)
            .HasForeignKey(e => e.ContextId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Attendance_Participant_Context");
    }
}


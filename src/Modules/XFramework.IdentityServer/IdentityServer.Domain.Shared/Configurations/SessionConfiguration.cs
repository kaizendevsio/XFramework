using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_SessionData");

        entity.ToTable("Session", "Identity");

        entity.HasIndex(e => e.SessionTypeId, "IX_tbl_SessionData_SessionTypeID");

        entity.HasIndex(e => e.CredentialId, "IX_tbl_SessionData_CredentialID");
        entity.HasIndex(e => new { e.TenantId, e.Status, e.ExpiresAt },
            "IX_Session_TenantId_Status_ExpiresAt");
        entity.HasIndex(e => new { e.TenantId, e.Status, e.RefreshTokenExpiresAt },
            "IX_Session_TenantId_Status_RefreshTokenExpiresAt");
        entity.HasIndex(e => new { e.TenantId, e.CreatedAt, e.Id },
            "IX_Session_TenantId_CreatedAt_Id");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.SessionData).HasMaxLength(2000);
        entity.Property(e => e.RefreshTokenHash).HasMaxLength(64);
        entity.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();
        entity.Property(e => e.Status).HasDefaultValue(XFramework.Domain.Shared.Enums.CurrentSessionState.Active);
        entity.Property(e => e.SessionTypeId).HasColumnName("SessionTypeID");
        entity.Property(e => e.CredentialId).HasColumnName("CredentialID");

        entity.HasOne(d => d.SessionType).WithMany(p => p.SessionData)
            .HasForeignKey(d => d.SessionTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_sessiondata_fk");

        // NOTE: Cross-module reference — IdentityCredential is from IdentityServer.Domain.Shared.Contracts
        entity.HasOne(d => d.Credential).WithMany(p => p.SessionData)
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_sessiondata_fk_1");
    }
}

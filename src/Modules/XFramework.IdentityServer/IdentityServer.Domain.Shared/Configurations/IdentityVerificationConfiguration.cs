using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerification>
{
    public void Configure(EntityTypeBuilder<IdentityVerification> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityVerifications");

        entity.ToTable("IdentityVerification", "Identity");

        entity.HasIndex(e => e.CredentialId, "IX_tbl_IdentityVerifications_CredentialID");

        entity.HasIndex(e => e.VerificationTypeId, "IX_tbl_IdentityVerifications_VerificationTypeID");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

        entity.Property(e => e.CredentialId).HasColumnName("CredentialID");
        entity.Property(e => e.StatusUpdatedOn).HasColumnType("time with time zone");
        entity.Property(e => e.Token).HasColumnType("character varying");
        entity.Property(e => e.VerificationTypeId).HasColumnName("VerificationTypeID");

        entity.HasOne(d => d.Credential).WithMany(p => p.IdentityVerifications)
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_UserVerifications_AuthID");

        entity.HasOne(d => d.VerificationType).WithMany(p => p.IdentityVerifications)
            .HasForeignKey(d => d.VerificationTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_UserVerifications_VerificationTypeID");
    }
}

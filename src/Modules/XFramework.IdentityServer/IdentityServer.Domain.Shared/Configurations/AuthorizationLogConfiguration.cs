using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class AuthorizationLogConfiguration : IEntityTypeConfiguration<AuthorizationLog>
{
    public void Configure(EntityTypeBuilder<AuthorizationLog> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityAuthorizationLogs");

        entity.ToTable("AuthorizationLog", "Audit");

        entity.HasIndex(e => e.CredentialId, "IX_tbl_IdentityAuthorizationLogs_CredentialID");
        entity.HasIndex(e => new { e.TenantId, e.CreatedAt },
            "IX_AuthorizationLog_TenantId_CreatedAt");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.DeviceName).HasMaxLength(50);

        entity.Property(e => e.Ipaddress)
            .HasMaxLength(64)
            .HasColumnName("IPAddress");
        entity.Property(e => e.LoginSource).HasMaxLength(50);
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        // NOTE: Cross-module reference — IdentityCredential is from IdentityServer.Domain.Shared.Contracts
        entity.HasOne(d => d.IdentityCredentials).WithMany(p => p.AuthorizationLogs)
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_userauthhistory_fk");
    }
}

using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityCredentialConfiguration : IEntityTypeConfiguration<IdentityCredential>
{
    public void Configure(EntityTypeBuilder<IdentityCredential> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityCredentials");

        entity.ToTable("IdentityCredential", "Identity");

        entity.HasIndex(e => e.IdentityInfoId, "IX_tbl_IdentityCredentials_IdentityInfoID");

        entity.HasIndex(e => e.AvatarStorageFileId, "IX_tbl_IdentityCredentials_AvatarStorageFileId");

        entity.HasIndex(e => new { e.TenantId, e.UserName }, "IX_IdentityCredential_TenantId_UserName")
            .IsUnique();
        entity.HasIndex(e => new { e.TenantId, e.CreatedAt, e.Id },
            "IX_IdentityCredential_TenantId_CreatedAt_Id");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.TenantId).HasColumnName("TenantId");

        entity.Property(e => e.IdentityInfoId).HasColumnName("IdentityInfoID");
        entity.Property(e => e.Token).HasMaxLength(512);
        entity.Property(e => e.UserAlias).HasMaxLength(100);
        entity.Property(e => e.UserName).HasMaxLength(100);
        entity.Property(e => e.AvatarUrl).HasMaxLength(2048);
        entity.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();

        entity.HasOne(d => d.Tenant).WithMany(p => p.IdentityCredentials)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tbl_identitycredentials___fk");

        entity.HasOne(d => d.IdentityInfo).WithMany(p => p.IdentityCredentials)
            .HasForeignKey(d => d.IdentityInfoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_identitycredentials_fk");

    }
}

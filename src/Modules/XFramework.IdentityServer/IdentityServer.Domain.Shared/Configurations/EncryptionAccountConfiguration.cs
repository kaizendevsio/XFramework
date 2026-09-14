using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityServer.Domain.Shared.Contracts;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class EncryptionAccountConfiguration : IEntityTypeConfiguration<EncryptionAccount>
{
    public void Configure(EntityTypeBuilder<EncryptionAccount> entity)
    {
        entity.ToTable("EncryptionAccount", "Identity");
        entity.HasKey(x => new { x.TenantId, x.CredentialId });
        entity.Property(x => x.DirectoryRevision).IsConcurrencyToken();
        entity.Property(x => x.RecoveryRevision).IsConcurrencyToken();
        entity.Property(x => x.RootPublicKey).HasMaxLength(16384);
        entity.Property(x => x.Roster).HasMaxLength(524288);
        entity.Property(x => x.DevicesJson).HasMaxLength(1048576);
        entity.Property(x => x.RecoveryArchive).HasMaxLength(2097152);
        entity.Property(x => x.PublicHistoryJson).HasColumnType("jsonb").HasDefaultValue("[]");
    }
}

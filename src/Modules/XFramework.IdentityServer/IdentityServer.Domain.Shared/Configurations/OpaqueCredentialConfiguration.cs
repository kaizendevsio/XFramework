using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IdentityServer.Domain.Shared.Contracts;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class OpaqueCredentialConfiguration : IEntityTypeConfiguration<OpaqueCredential>
{
    public void Configure(EntityTypeBuilder<OpaqueCredential> entity)
    {
        entity.ToTable("OpaqueCredential", "Identity");
        entity.HasKey(x => new { x.TenantId, x.CredentialId });
        entity.Property(x => x.Epoch).IsConcurrencyToken();
        entity.Property(x => x.Record).HasMaxLength(2048);
        entity.Property(x => x.WrappedRecovery).HasMaxLength(4096);
    }
}

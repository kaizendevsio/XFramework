using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public sealed class ServiceSigningKeyConfiguration : IEntityTypeConfiguration<ServiceSigningKey>
{
    public void Configure(EntityTypeBuilder<ServiceSigningKey> entity)
    {
        entity.ToTable("ServiceSigningKey", "Identity");
        entity.HasKey(x => x.Id);

        entity.Property(x => x.KeyId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Algorithm).HasMaxLength(32).IsRequired();
        entity.Property(x => x.PrivateKeyFileName).HasMaxLength(256).IsRequired();
        entity.Property(x => x.PublicKeyPem).IsRequired();
        entity.Property(x => x.CreatedBy).HasMaxLength(256);

        entity.HasIndex(x => x.KeyId).IsUnique();
        entity.HasIndex(x => x.IsActive)
            .IsUnique()
            .HasFilter("\"IsActive\" = true");
    }
}

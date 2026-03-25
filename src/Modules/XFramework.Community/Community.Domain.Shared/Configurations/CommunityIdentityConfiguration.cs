using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Configurations;

public class CommunityIdentityConfiguration : IEntityTypeConfiguration<CommunityIdentity>
{
    public void Configure(EntityTypeBuilder<CommunityIdentity> entity)
    {
        entity.HasKey(e => e.Id).HasName("socialidentity_pk");

        entity.ToTable("CommunityIdentity", "Community");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Alias).HasColumnType("character varying");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.HandleName).HasColumnType("character varying");
        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.LastActive).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Tagline).HasColumnType("character varying");

        entity.HasOne(d => d.Type).WithMany()
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("communityidentity_communityidentityentity_id_fk");

        entity.HasOne(d => d.Credential).WithMany()
            .HasForeignKey(d => d.CredentialId)
            .HasConstraintName("socialidentity_identitycredential_id_fk");
    }
}

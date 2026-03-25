using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Configurations;

public class CommunityConnectionConfiguration : IEntityTypeConfiguration<CommunityConnection>
{
    public void Configure(EntityTypeBuilder<CommunityConnection> entity)
    {
        entity.HasKey(e => e.Id).HasName("socialmediaconnection_pk");

        entity.ToTable("CommunityConnection", "Community");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasOne(d => d.Type).WithMany(p => p.CommunityConnections)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("metadata_metadataentity_id_fk");

        entity.HasOne(d => d.SourceSocialMediaIdentity)
            .WithMany(p => p.CommunityConnectionSourceSocialMediaIdentities)
            .HasForeignKey(d => d.SourceSocialMediaIdentityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmedia_sourcesocialmediaidentityid_id_fk");

        entity.HasOne(d => d.TargetSocialMediaIdentity)
            .WithMany(p => p.CommunityConnectionTargetSocialMediaIdentities)
            .HasForeignKey(d => d.TargetSocialMediaIdentityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmedia_targetsocialmediaidentityid_id_fk");
    }
}

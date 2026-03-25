using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Configurations;

public class CommunityContentConfiguration : IEntityTypeConfiguration<CommunityContent>
{
    public void Configure(EntityTypeBuilder<CommunityContent> entity)
    {
        entity.HasKey(e => e.Id).HasName("socialmediacontent_pk");

        entity.ToTable("CommunityContent", "Community");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Text).HasColumnType("character varying");
        entity.Property(e => e.Title).HasColumnType("character varying");

        entity.HasOne(d => d.CommunityGroup).WithMany(p => p.CommunityContentCommunityGroups)
            .HasForeignKey(d => d.CommunityGroupId)
            .HasConstraintName("communitycontent_communityidentity_id_fk");

        entity.HasOne(d => d.Type).WithMany(p => p.CommunityContents)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmediacontent_socialmediacontententity_id_fk");

        entity.HasOne(d => d.ParentContent).WithMany(p => p.InverseParentContent)
            .HasForeignKey(d => d.ParentContentId)
            .HasConstraintName("socialmediacontent_socialmediacontent_id_fk");

        entity.HasOne(d => d.SocialMediaIdentity).WithMany(p => p.CommunityContentSocialMediaIdentities)
            .HasForeignKey(d => d.SocialMediaIdentityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmediacontent_socialmediaidentity_id_fk");
    }
}

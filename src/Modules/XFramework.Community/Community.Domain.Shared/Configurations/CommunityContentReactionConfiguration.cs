using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Configurations;

public class CommunityContentReactionConfiguration : IEntityTypeConfiguration<CommunityContentReaction>
{
    public void Configure(EntityTypeBuilder<CommunityContentReaction> entity)
    {
        entity.HasKey(e => e.Id).HasName("socialmediacontentreaction_pk");

        entity.ToTable("CommunityContentReaction", "Community");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasOne(d => d.Content).WithMany(p => p.CommunityContentReactions)
            .HasForeignKey(d => d.ContentId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmediacontentreaction_socialmediacontent_id_fk");

        entity.HasOne(d => d.Type).WithMany(p => p.CommunityContentReactions)
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmediacontentreaction_contentreactionentity_id_fk");

        entity.HasOne(d => d.SocialMediaIdentity).WithMany(p => p.CommunityContentReactions)
            .HasForeignKey(d => d.SocialMediaIdentityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmediacontentreaction_socialmediaidentity_id_fk");
    }
}

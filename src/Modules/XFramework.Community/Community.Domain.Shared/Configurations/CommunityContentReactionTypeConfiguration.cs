using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Configurations;

public class CommunityContentReactionTypeConfiguration : IEntityTypeConfiguration<CommunityContentReactionType>
{
    public void Configure(EntityTypeBuilder<CommunityContentReactionType> entity)
    {
        entity.HasKey(e => e.Id).HasName("socialmediacontentreactionentity_pk");

        entity.ToTable("CommunityContentReactionType", "Community");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Emoji).HasColumnType("character varying");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasColumnType("character varying");
    }
}

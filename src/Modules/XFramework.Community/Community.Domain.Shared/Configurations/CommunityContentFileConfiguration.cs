using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Configurations;

public class CommunityContentFileConfiguration : IEntityTypeConfiguration<CommunityContentFile>
{
    public void Configure(EntityTypeBuilder<CommunityContentFile> entity)
    {
        entity.HasKey(e => e.Id).HasName("socialmediacontentfiles_pk");

        entity.ToTable("CommunityContentFiles", "Community");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasOne(d => d.Content).WithMany()
            .HasForeignKey(d => d.ContentId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmediacontentfiles_socialmediacontent_id_fk");

        entity.HasOne(d => d.Storage).WithMany()
            .HasForeignKey(d => d.StorageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("socialmediacontentfiles_storagefile_id_fk");
    }
}

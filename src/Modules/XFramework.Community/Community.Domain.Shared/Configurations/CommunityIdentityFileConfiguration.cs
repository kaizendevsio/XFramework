using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Community.Domain.Shared.Contracts;

namespace Community.Domain.Shared.Configurations;

public class CommunityIdentityFileConfiguration : IEntityTypeConfiguration<CommunityIdentityFile>
{
    public void Configure(EntityTypeBuilder<CommunityIdentityFile> entity)
    {
        entity.HasKey(e => e.Id).HasName("communityidentityfiles_pk");

        entity.ToTable("CommunityIdentityFile", "Community");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsEnabled)
            .IsRequired()
            .HasDefaultValueSql("true");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

        entity.HasOne(d => d.Type).WithMany()
            .HasForeignKey(d => d.TypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("communityidentityfile_communityidentityfileentity_id_fk");

        entity.HasOne(d => d.Identity).WithMany()
            .HasForeignKey(d => d.IdentityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("communityidentityfiles_communityidentity_id_fk");

        entity.HasOne(d => d.Storage).WithMany()
            .HasForeignKey(d => d.StorageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("communityidentityfiles_storagefile_id_fk");
    }
}

using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class IdentityInformationConfiguration : IEntityTypeConfiguration<IdentityInformation>
{
    public void Configure(EntityTypeBuilder<IdentityInformation> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_tbl_IdentityInfo");

        entity.ToTable("IdentityInformation", "Identity");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.TenantId).HasColumnName("TenantId");
        entity.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();
        entity.Property(e => e.FirstName).HasMaxLength(100);

        entity.Property(e => e.IdentityDescription).HasMaxLength(100);
        entity.Property(e => e.IdentityName).HasMaxLength(100);
        entity.Property(e => e.LastName).HasMaxLength(100);
        entity.Property(e => e.MiddleName).HasMaxLength(100);

        entity.HasOne(d => d.Tenant).WithMany(p => p.IdentityInformations)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("identityinformation_application_id_fk");
    }
}

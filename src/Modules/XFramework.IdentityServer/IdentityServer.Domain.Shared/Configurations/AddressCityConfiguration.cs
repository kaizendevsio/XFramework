using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Domain.Shared.Configurations;

public class AddressCityConfiguration : IEntityTypeConfiguration<AddressCity>
{
    public void Configure(EntityTypeBuilder<AddressCity> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_addresscity_pk");

        entity.ToTable("AddressCity", "GeoLocation");

        entity.HasIndex(e => e.Code, "tbl_addresscity_code_uindex").IsUnique();


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert


        entity.HasOne(d => d.ProvCode).WithMany(p => p.AddressCities)
            .HasPrincipalKey(p => p.Code)
            .HasForeignKey(d => d.ProvCodeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tbl_addresscity_tbl_addressprovince_code_fk");
    }
}

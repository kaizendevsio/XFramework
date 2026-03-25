using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CurrencyType = Wallets.Domain.Shared.Contracts.CurrencyType;

namespace Wallets.Domain.Shared.Configurations;

public class CurrencyTypeConfiguration : IEntityTypeConfiguration<CurrencyType>
{
    public void Configure(EntityTypeBuilder<CurrencyType> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_currency_pk");

        entity.ToTable("CurrencyType", "Finance");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.CurrencyIsoCode3).HasMaxLength(4);
        entity.Property(e => e.Description).HasMaxLength(500);

        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Name).HasMaxLength(256);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_ExchangeRate_pkey");

        entity.ToTable("ExchangeRate", "Finance");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Fee).HasPrecision(18, 10);

        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.SourceCurrencyTypeId).HasColumnName("SourceCurrencyTypeID");
        entity.Property(e => e.TargetCurrencyTypeId).HasColumnName("TargetCurrencyTypeID");
        entity.Property(e => e.Value).HasPrecision(18, 10);

        entity.HasOne(d => d.SourceCurrencyType).WithMany(p => p.ExchangeRateSourceCurrencyTypes)
            .HasForeignKey(d => d.SourceCurrencyTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("SourceCurrencyID");

        entity.HasOne(d => d.TargetCurrencyType).WithMany(p => p.ExchangeRateTargetCurrencyTypes)
            .HasForeignKey(d => d.TargetCurrencyTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("TargetCurrencyID");
    }
}

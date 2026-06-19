using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletFeeScheduleConfiguration : IEntityTypeConfiguration<WalletFeeSchedule>
{
    public void Configure(EntityTypeBuilder<WalletFeeSchedule> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletFeeSchedules_pkey");
        entity.ToTable("WalletFeeSchedule", "Wallet");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.Name).HasMaxLength(200);
        entity.Property(e => e.FixedFee).HasPrecision(24, 8);
        entity.Property(e => e.PercentageFee).HasPrecision(18, 10);
        entity.Property(e => e.MinimumFee).HasPrecision(24, 8);
        entity.Property(e => e.MaximumFee).HasPrecision(24, 8);
        entity.Property(e => e.EffectiveAt).HasDefaultValueSql("now()");

        entity.HasOne(e => e.WalletType)
            .WithMany()
            .HasForeignKey(e => e.WalletTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.IsEnabled, e.OperationType, e.WalletTypeId, e.CurrencyId });
        entity.HasIndex(e => new { e.TenantId, e.EffectiveAt, e.ExpiresAt });
    }
}

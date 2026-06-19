using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletReconciliationItemConfiguration : IEntityTypeConfiguration<WalletReconciliationItem>
{
    public void Configure(EntityTypeBuilder<WalletReconciliationItem> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletReconciliationItems_pkey");
        entity.ToTable("WalletReconciliationItem", "Wallet");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.CheckType).HasMaxLength(100);
        entity.Property(e => e.ExpectedAmount).HasPrecision(24, 8);
        entity.Property(e => e.ActualAmount).HasPrecision(24, 8);
        entity.Property(e => e.DriftAmount).HasPrecision(24, 8);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(200);
        entity.Property(e => e.DetailsJson).HasColumnType("jsonb");
        entity.Property(e => e.RepairSuggestion).HasMaxLength(4000);

        entity.HasOne(e => e.Run)
            .WithMany(r => r.Items)
            .HasForeignKey(e => e.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Wallet)
            .WithMany()
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.Status, e.CheckType });
        entity.HasIndex(e => new { e.TenantId, e.WalletId, e.Status });
        entity.HasIndex(e => new { e.RunId, e.Status });
    }
}

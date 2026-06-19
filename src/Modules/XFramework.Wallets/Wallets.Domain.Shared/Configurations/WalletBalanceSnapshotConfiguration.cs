using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletBalanceSnapshotConfiguration : IEntityTypeConfiguration<WalletBalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<WalletBalanceSnapshot> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletBalanceSnapshots_pkey");
        entity.ToTable("WalletBalanceSnapshot", "Wallet");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.Balance).HasPrecision(24, 8);
        entity.Property(e => e.AvailableBalance).HasPrecision(24, 8);
        entity.Property(e => e.TransferableBalance).HasPrecision(24, 8);
        entity.Property(e => e.DebitOnHoldBalance).HasPrecision(24, 8);
        entity.Property(e => e.CreditOnHoldBalance).HasPrecision(24, 8);
        entity.Property(e => e.TotalBalance).HasPrecision(24, 8);
        entity.Property(e => e.DriftAmount).HasPrecision(24, 8);

        entity.HasOne(e => e.Wallet)
            .WithMany()
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("tbl_WalletBalanceSnapshots_WalletId_fkey");

        entity.HasOne(e => e.LastOperation)
            .WithMany()
            .HasForeignKey(e => e.LastOperationId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("tbl_WalletBalanceSnapshots_LastOperationId_fkey");

        entity.HasOne(e => e.LastLedgerEntry)
            .WithMany()
            .HasForeignKey(e => e.LastLedgerEntryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("tbl_WalletBalanceSnapshots_LastLedgerEntryId_fkey");

        entity.HasIndex(e => new { e.TenantId, e.WalletId }).IsUnique();
        entity.HasIndex(e => new { e.TenantId, e.IsReconciled });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletLedgerEntryConfiguration : IEntityTypeConfiguration<WalletLedgerEntry>
{
    public void Configure(EntityTypeBuilder<WalletLedgerEntry> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletLedgerEntries_pkey");
        entity.ToTable("WalletLedgerEntry", "Wallet");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.Amount).HasPrecision(24, 8);
        entity.Property(e => e.Description).HasMaxLength(2000);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(200);
        entity.Property(e => e.CounterpartyType).HasMaxLength(100);
        entity.Property(e => e.CounterpartyReference).HasMaxLength(300);
        entity.Property(e => e.PreviousBalance).HasPrecision(24, 8);
        entity.Property(e => e.PreviousAvailableBalance).HasPrecision(24, 8);
        entity.Property(e => e.PreviousDebitOnHoldBalance).HasPrecision(24, 8);
        entity.Property(e => e.PreviousCreditOnHoldBalance).HasPrecision(24, 8);
        entity.Property(e => e.RunningBalance).HasPrecision(24, 8);
        entity.Property(e => e.RunningAvailableBalance).HasPrecision(24, 8);
        entity.Property(e => e.RunningDebitOnHoldBalance).HasPrecision(24, 8);
        entity.Property(e => e.RunningCreditOnHoldBalance).HasPrecision(24, 8);

        entity.HasOne(e => e.Operation)
            .WithMany(o => o.LedgerEntries)
            .HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("tbl_WalletLedgerEntries_OperationId_fkey");

        entity.HasOne(e => e.Wallet)
            .WithMany()
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("tbl_WalletLedgerEntries_WalletId_fkey");

        entity.HasOne(e => e.WalletTransaction)
            .WithMany()
            .HasForeignKey(e => e.WalletTransactionId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("tbl_WalletLedgerEntries_WalletTransactionId_fkey");

        entity.HasIndex(e => new { e.TenantId, e.OperationId, e.Sequence }).IsUnique();
        entity.HasIndex(e => new { e.TenantId, e.WalletId, e.CreatedAt });
        entity.HasIndex(e => new { e.TenantId, e.WalletTransactionId });
        entity.HasIndex(e => new { e.TenantId, e.ReferenceNumber });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletCaseConfiguration : IEntityTypeConfiguration<WalletCase>
{
    public void Configure(EntityTypeBuilder<WalletCase> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletCases_pkey");
        entity.ToTable("WalletCase", "Wallet");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.Amount).HasPrecision(24, 8);
        entity.Property(e => e.ExternalReference).HasMaxLength(200);
        entity.Property(e => e.ReasonCode).HasMaxLength(100);
        entity.Property(e => e.Reason).HasMaxLength(2000);

        entity.HasOne(e => e.Wallet)
            .WithMany()
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        entity.HasOne(e => e.OriginalOperation)
            .WithMany()
            .HasForeignKey(e => e.OriginalOperationId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.SettlementOperation)
            .WithMany()
            .HasForeignKey(e => e.SettlementOperationId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.OriginalTransaction)
            .WithMany()
            .HasForeignKey(e => e.OriginalTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.CaseType, e.Status, e.CreatedAt });
        entity.HasIndex(e => new { e.TenantId, e.ExternalReference })
            .IsUnique()
            .HasFilter("\"ExternalReference\" IS NOT NULL");
        entity.HasIndex(e => new { e.TenantId, e.WalletId, e.Status });
    }
}

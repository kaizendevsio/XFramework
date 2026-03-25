using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletTransactions_pkey");

        entity.ToTable("WalletTransaction", "Wallet");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Amount).HasPrecision(24, 8);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Description).HasMaxLength(10000);

        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.PreviousTotalBalance).HasPrecision(24, 8);
        entity.Property(e => e.Remarks).HasMaxLength(10000);
        entity.Property(e => e.RunningTotalBalance).HasPrecision(24, 8);

        entity.HasOne(d => d.Credential).WithMany()
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("UserAuthID");

        entity.HasOne(d => d.Wallet).WithMany()
            .HasForeignKey(d => d.WalletId)
            .HasConstraintName("SourceUserWalletId");
    }
}

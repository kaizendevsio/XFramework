using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletAddressConfiguration : IEntityTypeConfiguration<WalletAddress>
{
    public void Configure(EntityTypeBuilder<WalletAddress> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletAddresses_pkey");

        entity.ToTable("WalletAddress", "Wallet");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Address).HasMaxLength(512);
        entity.Property(e => e.Balance).HasPrecision(18, 10);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Remarks).HasMaxLength(100);

        entity.HasOne(d => d.Wallet).WithMany(p => p.WalletAddresses)
            .HasForeignKey(d => d.WalletId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("walletaddress_wallet_id_fk");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_Wallets_pkey");

        entity.ToTable("Wallet", "Wallet");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Balance).HasPrecision(24, 8);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Status).HasDefaultValue(WalletStatus.Active);

        entity.HasOne(d => d.Credential).WithMany()
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tbl_Wallets_CredentialId_fkey");

        entity.HasOne(d => d.WalletType).WithMany()
            .HasForeignKey(d => d.WalletTypeId)
            .HasConstraintName("tbl_Wallets_WalletTypeId_fkey");
    }
}

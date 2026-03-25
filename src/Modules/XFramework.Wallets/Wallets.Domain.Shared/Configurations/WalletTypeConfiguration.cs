using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletType = Wallets.Domain.Shared.Contracts.WalletType;

namespace Wallets.Domain.Shared.Configurations;

public class WalletTypeConfiguration : IEntityTypeConfiguration<WalletType>
{
    public void Configure(EntityTypeBuilder<WalletType> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletType_pkey");

        entity.ToTable("WalletType", "Wallet");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.TenantId);
        entity.Property(e => e.Code).HasMaxLength(9);
        entity.Property(e => e.CurrencyTypeId).HasColumnName("CurrencyTypeID");
        entity.Property(e => e.Desc).HasMaxLength(500);

        entity.Property(e => e.Name).HasMaxLength(20);

        entity.HasOne(d => d.Tenant).WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("tbl_walletTypes_tbl_applications_id_fk");

        entity.HasOne(d => d.CurrencyType).WithMany()
            .HasForeignKey(d => d.CurrencyTypeId)
            .HasConstraintName("CurrencyID");
    }
}

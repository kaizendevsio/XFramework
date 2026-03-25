using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WithdrawalRequest_pkey");

        entity.ToTable("WithdrawalRequest", "Wallet");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Address).HasMaxLength(10000);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Remarks).HasColumnType("character varying");
        entity.Property(e => e.Amount).HasPrecision(18, 10);

        entity.HasOne(d => d.Credential).WithMany()
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("WithdrawalRequest_CredentialId");

        entity.HasOne(d => d.Wallet).WithMany()
            .HasForeignKey(d => d.WalletId)
            .HasConstraintName("WithdrawalRequest_WalletId");
    }
}

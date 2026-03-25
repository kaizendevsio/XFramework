using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class DepositRequestConfiguration : IEntityTypeConfiguration<DepositRequest>
{
    public void Configure(EntityTypeBuilder<DepositRequest> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_DepositRequests_pkey");

        entity.ToTable("DepositRequest", "Wallet");


        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Address).HasMaxLength(10000);
        entity.Property(e => e.Amount).HasPrecision(18, 10);
        entity.Property(e => e.ConvenienceFee).HasPrecision(18, 10);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Discount).HasPrecision(18, 10);

        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.RawRequestData).HasMaxLength(10000);
        entity.Property(e => e.RawResponseData).HasMaxLength(5000);
        entity.Property(e => e.ReferenceNo).HasMaxLength(35);
        entity.Property(e => e.Remarks).HasMaxLength(10000);
        entity.Property(e => e.SystemFee).HasPrecision(18, 10);

        entity.HasOne(d => d.PaymentGateway).WithMany()
            .HasForeignKey(d => d.GatewayId)
            .HasConstraintName("DepositRequest_Gateway_ID_fk");

        entity.HasOne(d => d.Credential).WithMany()
            .HasForeignKey(d => d.CredentialId)
            .HasConstraintName("DepositRequest_CredentialId");

        entity.HasOne(d => d.SourceCurrency).WithMany()
            .HasForeignKey(d => d.SourceCurrencyId)
            .HasConstraintName("SourceCurrencyId");

        entity.HasOne(d => d.WalletType).WithMany()
            .HasForeignKey(d => d.WalletTypeId)
            .HasConstraintName("DepositRequest_WalletTypeId");
    }
}

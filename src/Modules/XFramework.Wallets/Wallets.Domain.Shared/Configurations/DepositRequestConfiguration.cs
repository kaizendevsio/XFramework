using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class DepositRequestConfiguration : IEntityTypeConfiguration<DepositRequest>
{
    public void Configure(EntityTypeBuilder<DepositRequest> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_DepositRequests_pkey");

        entity.ToTable("DepositRequest", "Wallet", table =>
        {
            table.HasCheckConstraint("CK_DepositRequest_PositiveAmount", "\"Amount\" IS NULL OR \"Amount\" > 0");
            table.HasCheckConstraint(
                "CK_DepositRequest_NonNegativeFees",
                "(\"ConvenienceFee\" IS NULL OR \"ConvenienceFee\" >= 0) AND " +
                "(\"SystemFee\" IS NULL OR \"SystemFee\" >= 0) AND " +
                "(\"Discount\" IS NULL OR \"Discount\" >= 0) AND " +
                "(\"RequestedFee\" IS NULL OR \"RequestedFee\" >= 0) AND " +
                "(\"CalculatedFee\" IS NULL OR \"CalculatedFee\" >= 0)");
        });


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
        entity.Property(e => e.ReferenceNo).HasMaxLength(200);
        entity.Property(e => e.Remarks).HasMaxLength(10000);
        entity.Property(e => e.SystemFee).HasPrecision(18, 10);
        entity.Property(e => e.ExternalReference).HasMaxLength(200);
        entity.Property(e => e.ProviderEventId).HasMaxLength(200);
        entity.Property(e => e.ProviderTransactionId).HasMaxLength(200);
        entity.Property(e => e.ProviderStatus).HasMaxLength(100);
        entity.Property(e => e.RequestedFee).HasPrecision(24, 8);
        entity.Property(e => e.CalculatedFee).HasPrecision(24, 8);
        entity.Property(e => e.FailureReason).HasMaxLength(4000);

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

        entity.HasOne(d => d.Wallet).WithMany()
            .HasForeignKey(d => d.WalletId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(d => d.Approval).WithMany()
            .HasForeignKey(d => d.ApprovalId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(d => d.SettlementOperation).WithMany()
            .HasForeignKey(d => d.SettlementOperationId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(d => d.SettlementTransaction).WithMany()
            .HasForeignKey(d => d.SettlementTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.WorkflowStatus, e.CreatedAt });
        entity.HasIndex(e => new { e.TenantId, e.ReferenceNo });
        entity.HasIndex(e => new { e.TenantId, e.ExternalReference })
            .IsUnique()
            .HasFilter("\"ExternalReference\" IS NOT NULL");
        entity.HasIndex(e => new { e.TenantId, e.ProviderEventId })
            .IsUnique()
            .HasFilter("\"ProviderEventId\" IS NOT NULL");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WithdrawalRequest_pkey");

        entity.ToTable("WithdrawalRequest", "Wallet", table =>
        {
            table.HasCheckConstraint("CK_WithdrawalRequest_PositiveAmount", "\"Amount\" IS NULL OR \"Amount\" > 0");
            table.HasCheckConstraint(
                "CK_WithdrawalRequest_NonNegativeFees",
                "(\"Fee\" IS NULL OR \"Fee\" >= 0) AND " +
                "(\"RequestedFee\" IS NULL OR \"RequestedFee\" >= 0) AND " +
                "(\"CalculatedFee\" IS NULL OR \"CalculatedFee\" >= 0)");
        });

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert
        entity.Property(e => e.Address).HasMaxLength(10000);
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Remarks).HasColumnType("character varying");
        entity.Property(e => e.Amount).HasPrecision(18, 10);
        entity.Property(e => e.Fee).HasPrecision(24, 8);
        entity.Property(e => e.RequestedFee).HasPrecision(24, 8);
        entity.Property(e => e.CalculatedFee).HasPrecision(24, 8);
        entity.Property(e => e.ExternalReference).HasMaxLength(200);
        entity.Property(e => e.ProviderEventId).HasMaxLength(200);
        entity.Property(e => e.ProviderTransactionId).HasMaxLength(200);
        entity.Property(e => e.ProviderStatus).HasMaxLength(100);
        entity.Property(e => e.RawRequestData).HasColumnType("jsonb");
        entity.Property(e => e.RawResponseData).HasColumnType("jsonb");
        entity.Property(e => e.FailureReason).HasMaxLength(4000);
        entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
        entity.Property(e => e.RequestHash).HasMaxLength(64);

        entity.HasOne(d => d.Credential).WithMany()
            .HasForeignKey(d => d.CredentialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("WithdrawalRequest_CredentialId");

        entity.HasOne(d => d.Wallet).WithMany()
            .HasForeignKey(d => d.WalletId)
            .HasConstraintName("WithdrawalRequest_WalletId");

        entity.HasOne(d => d.PaymentGateway).WithMany()
            .HasForeignKey(d => d.GatewayId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(d => d.Approval).WithMany()
            .HasForeignKey(d => d.ApprovalId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(d => d.HoldOperation).WithMany()
            .HasForeignKey(d => d.HoldOperationId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(d => d.SettlementOperation).WithMany()
            .HasForeignKey(d => d.SettlementOperationId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(d => d.SettlementTransaction).WithMany()
            .HasForeignKey(d => d.SettlementTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.WorkflowStatus, e.CreatedAt });
        entity.HasIndex(e => new { e.TenantId, e.ReferenceNumber });
        entity.HasIndex(e => new { e.TenantId, e.ExternalReference })
            .IsUnique()
            .HasFilter("\"ExternalReference\" IS NOT NULL");
        entity.HasIndex(e => new { e.TenantId, e.ProviderEventId })
            .IsUnique()
            .HasFilter("\"ProviderEventId\" IS NOT NULL");
        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}

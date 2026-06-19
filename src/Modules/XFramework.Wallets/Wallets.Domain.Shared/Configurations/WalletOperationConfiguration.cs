using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletOperationConfiguration : IEntityTypeConfiguration<WalletOperation>
{
    public void Configure(EntityTypeBuilder<WalletOperation> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletOperations_pkey");
        entity.ToTable("WalletOperation", "Wallet", table =>
        {
            table.HasCheckConstraint(
                "CK_WalletOperation_NonNegativeFees",
                "(\"RequestedFee\" IS NULL OR \"RequestedFee\" >= 0) AND " +
                "(\"CalculatedFee\" IS NULL OR \"CalculatedFee\" >= 0)");
            table.HasCheckConstraint(
                "CK_WalletOperation_RiskScoreRange",
                "\"RiskScore\" IS NULL OR (\"RiskScore\" >= 0 AND \"RiskScore\" <= 100)");
        });

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.Status).HasDefaultValue(WalletOperationStatus.Pending);
        entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
        entity.Property(e => e.RequestHash).HasMaxLength(128);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(200);
        entity.Property(e => e.CorrelationId).HasMaxLength(200);
        entity.Property(e => e.ExternalReference).HasMaxLength(200);
        entity.Property(e => e.RiskDecision).HasMaxLength(200);
        entity.Property(e => e.PolicyDecision).HasMaxLength(2000);
        entity.Property(e => e.PolicyDecisionJson).HasColumnType("jsonb");
        entity.Property(e => e.RiskTier).HasMaxLength(100);
        entity.Property(e => e.RiskScore).HasPrecision(18, 8);
        entity.Property(e => e.RequestedFee).HasPrecision(24, 8);
        entity.Property(e => e.CalculatedFee).HasPrecision(24, 8);
        entity.Property(e => e.Reason).HasMaxLength(2000);
        entity.Property(e => e.FailureMessage).HasMaxLength(4000);

        entity.HasOne(e => e.OriginalOperation)
            .WithMany()
            .HasForeignKey(e => e.OriginalOperationId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        entity.HasIndex(e => new { e.TenantId, e.ReferenceNumber });
        entity.HasIndex(e => new { e.TenantId, e.OperationType, e.Status });
        entity.HasIndex(e => new { e.TenantId, e.ActorCredentialId });
        entity.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt });
        entity.HasIndex(e => new { e.TenantId, e.ExternalReference });
    }
}

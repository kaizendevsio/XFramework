using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletApprovalRequestConfiguration : IEntityTypeConfiguration<WalletApprovalRequest>
{
    public void Configure(EntityTypeBuilder<WalletApprovalRequest> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletApprovalRequests_pkey");
        entity.ToTable("WalletApprovalRequest", "Wallet");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.Amount).HasPrecision(24, 8);
        entity.Property(e => e.Reason).HasMaxLength(2000);
        entity.Property(e => e.DecisionReason).HasMaxLength(2000);
        entity.Property(e => e.AuditMetadataJson).HasColumnType("jsonb");
        entity.Property(e => e.RequestedAt).HasDefaultValueSql("now()");

        entity.HasOne(e => e.Wallet)
            .WithMany()
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Operation)
            .WithOne(o => o.Approval)
            .HasForeignKey<WalletApprovalRequest>(e => e.OperationId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.Status, e.OperationType, e.RequestedAt });
        entity.HasIndex(e => new { e.TenantId, e.RequesterCredentialId });
        entity.HasIndex(e => new { e.TenantId, e.ApproverCredentialId });
    }
}

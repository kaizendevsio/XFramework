using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletOutboxMessageConfiguration : IEntityTypeConfiguration<WalletOutboxMessage>
{
    public void Configure(EntityTypeBuilder<WalletOutboxMessage> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletOutboxMessages_pkey");
        entity.ToTable("WalletOutboxMessage", "Wallet");

        entity.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.EventType).HasMaxLength(300);
        entity.Property(e => e.AggregateType).HasMaxLength(200);
        entity.Property(e => e.PayloadJson).HasColumnType("jsonb");
        entity.Property(e => e.HeadersJson).HasColumnType("jsonb");
        entity.Property(e => e.LastError).HasMaxLength(4000);
        entity.Property(e => e.LockedBy).HasMaxLength(200);
        entity.Property(e => e.Status).HasDefaultValue(WalletOutboxStatus.Pending);

        entity.HasOne(e => e.Operation)
            .WithMany(o => o.OutboxMessages)
            .HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("tbl_WalletOutboxMessages_OperationId_fkey");

        entity.HasIndex(e => new { e.TenantId, e.Status, e.NextAttemptAt });
        entity.HasIndex(e => new { e.TenantId, e.Status, e.LockedUntil, e.NextAttemptAt });
        entity.HasIndex(e => new { e.TenantId, e.OperationId });
        entity.HasIndex(e => new { e.TenantId, e.AggregateType, e.AggregateId });
        entity.HasIndex(e => new { e.TenantId, e.OperationId, e.EventType })
            .IsUnique()
            .HasFilter("\"OperationId\" IS NOT NULL");
    }
}

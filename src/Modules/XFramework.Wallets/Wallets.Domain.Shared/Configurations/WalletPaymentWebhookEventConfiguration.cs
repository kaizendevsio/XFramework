using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletPaymentWebhookEventConfiguration : IEntityTypeConfiguration<WalletPaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<WalletPaymentWebhookEvent> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletPaymentWebhookEvents_pkey");
        entity.ToTable("WalletPaymentWebhookEvent", "Wallet");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.ProviderKey).HasMaxLength(100);
        entity.Property(e => e.ExternalEventId).HasMaxLength(200);
        entity.Property(e => e.ExternalReference).HasMaxLength(200);
        entity.Property(e => e.ProviderTransactionId).HasMaxLength(200);
        entity.Property(e => e.ProviderStatus).HasMaxLength(100);
        entity.Property(e => e.SignatureScheme).HasMaxLength(100);
        entity.Property(e => e.HeadersHash).HasMaxLength(128);
        entity.Property(e => e.RawPayloadJson).HasColumnType("jsonb");
        entity.Property(e => e.ProcessingError).HasMaxLength(4000);
        entity.Property(e => e.ReceivedAt).HasDefaultValueSql("now()");

        entity.HasOne(e => e.DepositRequest)
            .WithMany()
            .HasForeignKey(e => e.DepositRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.WithdrawalRequest)
            .WithMany()
            .HasForeignKey(e => e.WithdrawalRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Operation)
            .WithMany()
            .HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.ProviderKey, e.ExternalEventId })
            .IsUnique()
            .HasFilter("\"ExternalEventId\" <> ''");
        entity.HasIndex(e => new { e.TenantId, e.ExternalReference });
        entity.HasIndex(e => new { e.TenantId, e.ProcessingStatus, e.ReceivedAt });
    }
}

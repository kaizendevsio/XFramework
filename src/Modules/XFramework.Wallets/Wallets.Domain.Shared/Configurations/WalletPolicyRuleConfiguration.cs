using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletPolicyRuleConfiguration : IEntityTypeConfiguration<WalletPolicyRule>
{
    public void Configure(EntityTypeBuilder<WalletPolicyRule> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletPolicyRules_pkey");
        entity.ToTable("WalletPolicyRule", "Wallet");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.Name).HasMaxLength(200);
        entity.Property(e => e.MaxSingleTransactionAmount).HasPrecision(24, 8);
        entity.Property(e => e.DailyVelocityLimit).HasPrecision(24, 8);
        entity.Property(e => e.MonthlyVelocityLimit).HasPrecision(24, 8);
        entity.Property(e => e.ApprovalThreshold).HasPrecision(24, 8);
        entity.Property(e => e.RiskTier).HasMaxLength(100);
        entity.Property(e => e.DecisionCode).HasMaxLength(100);
        entity.Property(e => e.EffectiveAt).HasDefaultValueSql("now()");

        entity.HasOne(e => e.WalletType)
            .WithMany()
            .HasForeignKey(e => e.WalletTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.TenantId, e.IsEnabled, e.OperationType, e.WalletTypeId });
        entity.HasIndex(e => new { e.TenantId, e.EffectiveAt, e.ExpiresAt });
    }
}

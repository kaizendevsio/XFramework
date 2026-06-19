using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletReconciliationRunConfiguration : IEntityTypeConfiguration<WalletReconciliationRun>
{
    public void Configure(EntityTypeBuilder<WalletReconciliationRun> entity)
    {
        entity.HasKey(e => e.Id).HasName("tbl_WalletReconciliationRuns_pkey");
        entity.ToTable("WalletReconciliationRun", "Wallet");

        entity.Property(e => e.Id).HasColumnName("ID").HasDefaultValueSql("(uuid_generate_v4())");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.IsDeleted).HasDefaultValueSql("false");
        entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");
        entity.Property(e => e.Error).HasMaxLength(4000);

        entity.HasIndex(e => new { e.TenantId, e.Status, e.StartedAt });
    }
}

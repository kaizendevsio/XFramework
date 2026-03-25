using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletTransferConfiguration : IEntityTypeConfiguration<WalletTransfer>
{
    public void Configure(EntityTypeBuilder<WalletTransfer> entity)
    {
        entity.ToTable("WalletTransfer", "Wallet");
    }
}

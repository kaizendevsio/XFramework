using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Configurations;

public class WalletTransactionLineItemConfiguration : IEntityTypeConfiguration<WalletTransactionLineItem>
{
    public void Configure(EntityTypeBuilder<WalletTransactionLineItem> entity)
    {
        entity.ToTable("WalletTransactionLineItem", "Wallet");
    }
}

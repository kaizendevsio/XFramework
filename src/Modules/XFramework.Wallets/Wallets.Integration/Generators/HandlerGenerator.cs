using Wallets.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Wallets.Integration.Generators;

[StreamFlowWrapper("Wallets.Domain.Shared.Contracts", new[]
{
    nameof(Wallet),
    nameof(WalletType),
    nameof(WalletAddress),
    nameof(WalletTransaction),
    nameof(DepositRequest),
    nameof(WithdrawalRequest),
})]
public static class WalletsServiceWrapper;

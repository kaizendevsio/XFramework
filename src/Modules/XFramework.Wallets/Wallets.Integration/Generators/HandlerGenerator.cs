using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Wallets.Integration.Generators;

[StreamFlowWrapper("XFramework.Domain.Shared.Contracts",new[] {
    nameof(Wallet),
    nameof(WalletType),
    nameof(WalletAddress),
    nameof(WalletTransaction),
    nameof(DepositRequest),
    nameof(WithdrawalRequest),
})]
public static class WalletsServiceWrapper;
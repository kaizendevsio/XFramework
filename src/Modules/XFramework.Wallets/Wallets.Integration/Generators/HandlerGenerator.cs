using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Wallets.Integration.Generators;

[StreamFlowWrapper("XFramework.Domain.Shared.Contracts",new[] {
    nameof(Wallet),
    nameof(WalletType),
    nameof(WalletAddress),
    nameof(WalletTransaction),
})]
public static class WalletsServiceWrapper;
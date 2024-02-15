using XFramework.Domain.Generic.Contracts;
using XFramework.Integration.Attributes;

namespace Wallets.Integration.Generators;

[StreamFlowWrapper("XFramework.Domain.Generic.Contracts",new[] {
    nameof(Wallet),
    nameof(WalletType),
    nameof(WalletAddress),
    nameof(WalletTransaction),
})]
public static class WalletsServiceWrapper;
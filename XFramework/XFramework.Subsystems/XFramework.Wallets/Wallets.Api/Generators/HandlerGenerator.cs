using XFramework.Core.Attributes;
using XFramework.Domain.Generic.Contracts;

namespace Wallets.Api.Generators;

[GenerateApiFromNamespace("XFramework.Domain.Generic.Contracts",new[] {
    nameof(Wallet),
    nameof(WalletType),
    nameof(WalletAddress),
    nameof(WalletTransaction),
})]
public static partial class WalletsApiGenerator;
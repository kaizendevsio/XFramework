using XFramework.Core.Attributes;
using XFramework.Domain.Shared.Contracts;

namespace Wallets.Api.Generators;

[GenerateApiFromNamespace("XFramework.Domain.Shared.Contracts",new[] {
    nameof(Wallet),
    nameof(WalletType),
    nameof(WalletAddress),
    nameof(WalletTransaction),
})]
public static partial class WalletsApiGenerator;
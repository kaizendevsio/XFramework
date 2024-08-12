using XFramework.Core.Attributes;

namespace Wallets.Api.Endpoints;

[GenerateEndpoints("XFramework.Domain.Shared.Contracts",new[] {
    nameof(Wallet),
    nameof(WalletType),
    nameof(WalletAddress),
    nameof(WalletTransaction),
    nameof(DepositRequest),
    nameof(WithdrawalRequest),
})]
public static partial class WalletsEndpoints;
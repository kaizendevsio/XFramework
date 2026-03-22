using Wallets.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Wallets.Integration.Generators;

// Wallet is explicitly listed because it doesn't have [GenerateEndpoints] (manual service).
// Other entities are auto-discovered from [GenerateEndpoints], but we keep the full list
// for reliability since the old ISourceGenerator API has timing issues with referenced assemblies.
[StreamFlowWrapper("Wallets.Domain.Shared.Contracts", new[]
{
    nameof(Wallet),
    nameof(WalletType),
    nameof(WalletAddress),
    nameof(WalletTransaction),
    nameof(WalletTransactionLineItem),
    nameof(WalletTransfer),
    nameof(CurrencyType),
    nameof(ExchangeRate),
    nameof(DepositRequest),
    nameof(WithdrawalRequest),
})]
public static class WalletsServiceWrapper;

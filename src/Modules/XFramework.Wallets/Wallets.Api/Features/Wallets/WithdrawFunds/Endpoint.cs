using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.WithdrawFunds;

/// <summary>
/// Withdraw Funds (Decrement) endpoint
/// </summary>
public static class WithdrawFundsEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Transact])]
    [MapPost("/api/wallets/withdraw-funds", Tags = ["Wallets"],
        Summary = "Withdraw funds from a wallet",
        Description = "Decrements (subtracts from) a wallet's balance. Supports both immediate and on-hold decrements. Validates sufficient available balance.",
        RequireAuthorization = true,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.Transact],
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        DecrementWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.DecrementBalanceAsync(request, ct);
    }
}

using Wallets.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.WithdrawFunds;

/// <summary>
/// Withdraw Funds (Decrement) endpoint
/// </summary>
public static class WithdrawFundsEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/withdraw-funds", Tags = ["Wallets"],
        Summary = "Withdraw funds from a wallet",
        Description = "Decrements (subtracts from) a wallet's balance. Supports both immediate and on-hold decrements. Validates sufficient available balance.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        DecrementWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.DecrementBalanceAsync(request, ct);
    }
}

using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.AddFunds;

/// <summary>
/// Add Funds (Increment) endpoint
/// </summary>
public static class AddFundsEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    [MapPost("/api/wallets/add-funds", Tags = ["Wallets"],
        Summary = "Add funds to a wallet",
        Description = "Increments (adds to) a wallet's balance. Supports both immediate and on-hold increments. Automatically creates wallet if WalletTypeId is provided and wallet doesn't exist.",
        RequireAuthorization = true,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update],
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        IncrementWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.IncrementBalanceAsync(request, ct);
    }
}

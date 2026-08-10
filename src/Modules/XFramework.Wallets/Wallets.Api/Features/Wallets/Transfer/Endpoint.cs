using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.Transfer;

/// <summary>
/// Transfer Funds endpoint
/// </summary>
public static class TransferEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update])]
    [MapPost("/api/wallets/transfer", Tags = ["Wallets"],
        Summary = "Transfer funds between wallets",
        Description = "Transfers funds from one wallet to another. Handles fee deduction based on TransferDeductionType. Automatically creates recipient wallet if it doesn't exist.",
        RequireAuthorization = true,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.Update],
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        TransferWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.TransferAsync(request, ct);
    }
}

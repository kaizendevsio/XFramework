using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.Convert;

/// <summary>
/// Convert Currency endpoint
/// </summary>
public static class ConvertEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Transact])]
    [MapPost("/api/wallets/convert", Tags = ["Wallets"],
        Summary = "Convert funds between wallet types",
        Description = "Converts funds from one wallet type to another for the same credential. Handles fee deduction based on TransferDeductionType. Automatically creates target wallet if it doesn't exist.",
        RequireAuthorization = true,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.Transact],
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        ConvertWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.ConvertWalletAsync(request, ct);
    }
}

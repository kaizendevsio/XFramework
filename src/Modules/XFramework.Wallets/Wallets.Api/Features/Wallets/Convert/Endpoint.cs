using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.Convert;

/// <summary>
/// Convert Currency endpoint
/// </summary>
public static class ConvertEndpoint
{
    [StreamFlowHandler]
    [MapPost("/api/wallets/convert", Tags = ["Wallets"],
        Summary = "Convert funds between wallet types",
        Description = "Converts funds from one wallet type to another for the same credential. Handles fee deduction based on TransferDeductionType. Automatically creates target wallet if it doesn't exist.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        ConvertWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.ConvertWalletAsync(request, ct);
    }
}
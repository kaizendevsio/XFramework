using FluentValidation;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.Freeze;

public static class FreezeWalletEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/freeze", Tags = ["Wallets"],
        Summary = "Freeze a wallet",
        Description = "Freezes a wallet, preventing all financial operations (transfer, increment, decrement, convert).",
        RequireAuthorization = true,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage],
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        FreezeWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.FreezeWalletAsync(request, ct);
    }
}

public class FreezeWalletValidator : AbstractValidator<FreezeWalletRequest>
{
    public FreezeWalletValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("Wallet ID is required");
    }
}

using FluentValidation;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.Unfreeze;

public static class UnfreezeWalletEndpoint
{
    [BoltHandler(RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage])]
    [MapPost("/api/wallets/unfreeze", Tags = ["Wallets"],
        Summary = "Unfreeze a wallet",
        Description = "Unfreezes a wallet, restoring it to Active status and allowing financial operations.",
        RequireAuthorization = true,
        RequiredActorCapabilities = [WalletAuthorizationCapabilities.Manage],
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        UnfreezeWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.UnfreezeWalletAsync(request, ct);
    }
}

public class UnfreezeWalletValidator : AbstractValidator<UnfreezeWalletRequest>
{
    public UnfreezeWalletValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("Wallet ID is required");
    }
}

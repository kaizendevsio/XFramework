using FluentValidation;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.Unfreeze;

public static class UnfreezeWalletEndpoint
{
    [StreamFlowHandler]
    [MapPost("/api/wallets/unfreeze", Tags = ["Wallets"],
        Summary = "Unfreeze a wallet",
        Description = "Unfreezes a wallet, restoring it to Active status and allowing financial operations.",
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

using FluentValidation;
using Wallets.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.Close;

public static class CloseWalletEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/close", Tags = ["Wallets"],
        Summary = "Close a wallet",
        Description = "Closes an empty wallet, preventing future financial operations.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        CloseWalletRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.CloseWalletAsync(request, ct);
    }
}

public class CloseWalletValidator : AbstractValidator<CloseWalletRequest>
{
    public CloseWalletValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("Wallet ID is required");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must be 500 characters or fewer");
    }
}

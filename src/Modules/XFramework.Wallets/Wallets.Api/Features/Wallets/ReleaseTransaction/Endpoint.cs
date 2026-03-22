using FluentValidation;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.ReleaseTransaction;

/// <summary>
/// Release Transaction endpoint
/// </summary>
public static class ReleaseTransactionEndpoint
{
    [StreamFlowHandler]
    [MapPost("/api/wallets/release-transaction", Tags = ["Wallets"],
        Summary = "Release a held transaction",
        Description = "Releases a transaction that was previously placed on hold. Moves the amount from on-hold balances to available balances.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        ReleaseTransactionRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.ReleaseTransactionAsync(request, ct);
    }
}

public class ReleaseTransactionValidator : AbstractValidator<ReleaseTransactionRequest>
{
    public ReleaseTransactionValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Transaction ID is required");
    }
}
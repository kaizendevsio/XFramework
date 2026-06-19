using FluentValidation;
using Wallets.Api.Services;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Wallets.Api.Features.Wallets.ReverseTransaction;

public static class ReverseTransactionEndpoint
{
    [BoltHandler]
    [MapPost("/api/wallets/reverse-transaction", Tags = ["Wallets"],
        Summary = "Reverse a transaction",
        Description = "Reverses a single transaction or a full transfer (paired). Creates inverse transactions and updates balances.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        ReverseTransactionRequest request,
        IWalletOperationsService walletService,
        CancellationToken ct)
    {
        return await walletService.ReverseTransactionAsync(request, ct);
    }
}

public class ReverseTransactionValidator : AbstractValidator<ReverseTransactionRequest>
{
    public ReverseTransactionValidator()
    {
        RuleFor(x => x)
            .Must(x => x.TransactionId != Guid.Empty || x.WalletTransferId != Guid.Empty)
            .WithMessage("Either TransactionId or WalletTransferId must be provided");
    }
}

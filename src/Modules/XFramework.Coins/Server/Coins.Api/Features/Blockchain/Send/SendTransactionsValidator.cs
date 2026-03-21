using Coins.Api.BusinessObjects;

namespace Coins.Api.Features.Blockchain.Send;

/// <summary>
/// Validator for bulk send transactions request
/// </summary>
public class SendTransactionsValidator : AbstractValidator<List<BtcTransactionBO>>
{
    public SendTransactionsValidator()
    {
        RuleFor(x => x)
            .NotNull()
            .WithMessage("Transaction list cannot be null");

        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("Transaction list cannot be empty");

        RuleForEach(x => x).ChildRules(transaction =>
        {
            transaction.RuleFor(t => t.BtcAddress)
                .NotEmpty()
                .WithMessage("Bitcoin address is required");

            transaction.RuleFor(t => t.BtcAmount)
                .GreaterThan(0)
                .WithMessage("Amount must be greater than zero");
        });
    }
}
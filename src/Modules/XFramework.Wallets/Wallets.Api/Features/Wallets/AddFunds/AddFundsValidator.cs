using FluentValidation;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.AddFunds;

/// <summary>
/// Validator for IncrementWalletRequest
/// </summary>
public class AddFundsValidator : AbstractValidator<IncrementWalletRequest>
{
    public AddFundsValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Fee)
            .GreaterThanOrEqualTo(0).WithMessage("Fee cannot be negative");

        When(x => x.WalletId == Guid.Empty, () =>
        {
            RuleFor(x => x.WalletTypeId)
                .NotEmpty().WithMessage("Wallet Type ID is required when Wallet ID is not provided");
        });
    }
}
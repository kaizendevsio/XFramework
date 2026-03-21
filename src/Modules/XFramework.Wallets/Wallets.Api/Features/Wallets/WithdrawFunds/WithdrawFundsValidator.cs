using FluentValidation;
using Wallets.Domain.Shared.Contracts.Requests;

namespace Wallets.Api.Features.Wallets.WithdrawFunds;

/// <summary>
/// Validator for DecrementWalletRequest
/// </summary>
public class WithdrawFundsValidator : AbstractValidator<DecrementWalletRequest>
{
    public WithdrawFundsValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("Wallet ID is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Fee)
            .GreaterThanOrEqualTo(0).WithMessage("Fee cannot be negative");
    }
}
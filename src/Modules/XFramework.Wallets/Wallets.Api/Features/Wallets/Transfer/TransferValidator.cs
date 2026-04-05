using FluentValidation;

namespace Wallets.Api.Features.Wallets.Transfer;

/// <summary>
/// Validator for TransferWalletRequest
/// </summary>
public class TransferValidator : AbstractValidator<TransferWalletRequest>
{
    public TransferValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Sender Credential ID is required");

        RuleFor(x => x.RecipientCredentialId)
            .NotEmpty().WithMessage("Recipient Credential ID is required");

        RuleFor(x => x.WalletTypeId)
            .NotEmpty().WithMessage("Wallet Type ID is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0");

        RuleFor(x => x.Fee)
            .GreaterThanOrEqualTo(0).WithMessage("Fee cannot be negative");

        RuleFor(x => x.RecipientCredentialId)
            .NotEqual(x => x.CredentialId)
            .WithMessage("Cannot transfer to the same credential");
    }
}
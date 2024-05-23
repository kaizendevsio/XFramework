using FluentValidation;
using XFramework.Blazor.Core.Features.Wallet;
using XFramework.Blazor.Entity.Models.Requests.Wallet;

namespace XFramework.Blazor.Entity.Validations.Wallet;

public class TransferWalletValidator : AbstractValidator<WalletState.TransferWallet>
{
    public TransferWalletValidator()
    {
        RuleFor(x => x.Amount)
            .NotEmpty()
            .WithMessage("Amount is required")
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0");

        RuleFor(x => x.RecipientCredentialId)
            .NotEmpty()
            .WithMessage("Receiver is required");

        RuleFor(x => x.SenderCredentialId)
            .NotEmpty()
            .WithMessage("Sender is required");
        
        RuleFor(x => x.WalletTypeId)
            .NotEmpty()
            .WithMessage("Wallet Type is required");

        RuleFor(x => x.SenderCredentialId)
            .NotEqual(x => x.RecipientCredentialId)
            .WithMessage("Sender and Receiver must be different");

    }
}
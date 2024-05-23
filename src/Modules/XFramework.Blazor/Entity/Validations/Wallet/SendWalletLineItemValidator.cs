using FluentValidation;
using XFramework.Blazor.Entity.Models.Requests.Wallet;

namespace XFramework.Blazor.Entity.Validations.Wallet;

public class SendWalletLineItemValidator : AbstractValidator<SendWalletLineItem>
{
    public SendWalletLineItemValidator()
    {
        RuleFor(x => x.Amount)
            .NotEmpty()
            .WithMessage("Amount is required")
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0");
        
        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required");
    }
}
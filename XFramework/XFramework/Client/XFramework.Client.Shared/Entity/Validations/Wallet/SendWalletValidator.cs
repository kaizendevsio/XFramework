using FluentValidation;
using XFramework.Client.Shared.Entity.Models.Requests.Wallet;

namespace XFramework.Client.Shared.Entity.Validations.Wallet;

public class SendWalletValidator : AbstractValidator<SendWalletRequest>
{
    public SendWalletValidator()
    {
        RuleFor(i => i.Amount)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("Amount is required");
        RuleFor(i => i.WalletEntityGuid)
            .NotEmpty()
            .WithMessage("Wallet type is required");
    }
}
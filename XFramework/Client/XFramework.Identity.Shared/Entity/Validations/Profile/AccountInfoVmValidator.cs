using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.Profile;

namespace XFramework.Identity.Shared.Entity.Validations.Profile
{
    public class AccountInfoVmValidator: AbstractValidator<AccountInfoVm>
    {
        public AccountInfoVmValidator()
        {
            RuleFor(x => x.Username).NotEmpty()
                .WithMessage("Enter your first name");
            RuleFor(x => x.EmailAddress).NotEmpty()
                .WithMessage("Enter your email address");
        }
    }
}
using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.User;
using XFramework.Identity.Shared.Entity.ViewModels.User.Session;

namespace XFramework.Identity.Shared.Entity.Validations.Session
{
    public class SignInVmValidator : AbstractValidator<SignUpVm>
    {
        public SignInVmValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty()
                .WithMessage("Enter your first name");
            RuleFor(x => x.LastName).NotEmpty()
                .WithMessage("Enter your last name");
            RuleFor(x => x.Email).NotEmpty()
                .WithMessage("Enter your email address");
            RuleFor(x => x.PasswordString).NotEmpty()
                .WithMessage("Enter your password")
                .MinimumLength(8)
                .WithMessage("Minimum password length is 8 characters");
            RuleFor(x => x.ConfirmPasswordString)
                .NotEmpty()
                .WithMessage("Re-enter your password")
                .MinimumLength(8)
                .WithMessage("Minimum password length is 8 characters")
                .Equal(x => x.PasswordString)
                .WithMessage("Password must match");

            RuleFor(x => x.Agree).Must(x => x.Equals(true))
                .WithMessage("You must read the terms and conditions");
        }
    }
}
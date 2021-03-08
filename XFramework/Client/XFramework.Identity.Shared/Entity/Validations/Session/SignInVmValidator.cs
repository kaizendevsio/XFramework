using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.User;
using XFramework.Identity.Shared.Entity.ViewModels.User.Session;

namespace XFramework.Identity.Shared.Entity.Validations.Session
{
    public class UserVmValidator : AbstractValidator<SignInVm>
    {
        public UserVmValidator()
        {
            RuleFor(x => x.Username).NotEmpty()
                .WithMessage("Username is required");
            RuleFor(x => x.PasswordString).NotEmpty()
                .WithMessage("Enter your password")
                .MinimumLength(8)
                .WithMessage("Minimum password length is 8 characters");



        }
    }
}
using FluentValidation;
using XFramework.Client.Shared.Entity.Models.Requests.Session;

namespace XFramework.Client.Shared.Entity.Validations.Session
{
    public class UserVmValidator : AbstractValidator<SignInRequest>
    {
        public UserVmValidator()
        {
            RuleFor(x => x.Username).NotEmpty()
                .WithMessage("Please provide the required information.");
            RuleFor(x => x.Password).NotEmpty()
                .WithMessage("Enter your password")
                .MinimumLength(8)
                .WithMessage("Minimum password length is 8 characters");
        }
    }
}
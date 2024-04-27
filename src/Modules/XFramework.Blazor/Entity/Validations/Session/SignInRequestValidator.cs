using FluentValidation;
using XFramework.Blazor.Entity.Models.Requests.Session;

namespace XFramework.Blazor.Entity.Validations.Session
{
    public class UserVmValidator : AbstractValidator<SignInRequest>
    {
        public UserVmValidator()
        {
            RuleFor(x => x.UserName).NotEmpty()
                .WithMessage("Please provide the required information.");
            RuleFor(x => x.Password).NotEmpty()
                .WithMessage("Enter your password")
                .MinimumLength(8)
                .WithMessage("Minimum password length is 8 characters");
        }
    }
}
using FluentValidation;
using XFramework.Blazor.Entity.Models.Requests.Session;

namespace XFramework.Blazor.Entity.Validations.Session;

public class ForgotPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("Minimum password length is 8 characters")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$")
            .WithMessage("Password must contain at least one lowercase letter, one uppercase letter, one digit, and one special character");
        RuleFor(i => i.PasswordConfirmation)
            .NotEmpty()
            .Equal(i => i.Password)
            .WithMessage("Passwords does not match");
    }
}
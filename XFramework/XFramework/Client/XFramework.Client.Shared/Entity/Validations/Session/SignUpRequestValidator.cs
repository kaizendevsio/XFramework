using FluentValidation;
using XFramework.Client.Shared.Entity.Models.Requests.Session;

namespace XFramework.Client.Shared.Entity.Validations.Session;

public class SignUpRequestValidator : AbstractValidator<SignUpRequest>
{
    public SignUpRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty();
        RuleFor(x => x.LastName)
            .NotEmpty();
        RuleFor(x => x.UserName)
            .NotEmpty();
        RuleFor(x => x.EmailAddress)
            .NotEmpty();
        RuleFor(x => x.PhoneNumber)
            .NotEmpty();
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
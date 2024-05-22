using FluentValidation;
using XFramework.Blazor.Entity.Models.Requests.Session;

namespace XFramework.Blazor.Entity.Validations.Session;

public class ForgotPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.PhoneEmailUsername)
            .NotEmpty()
            .Matches(@"^09\d{9}$")
            .WithMessage("Phone must be a valid phone number in the format 09000000000.");
    }
}
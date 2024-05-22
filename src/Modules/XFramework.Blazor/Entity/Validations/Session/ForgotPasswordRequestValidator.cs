using FluentValidation;
using XFramework.Blazor.Entity.Models.Requests.Session;

namespace XFramework.Blazor.Entity.Validations.Session;

public class ForgotPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.PhoneEmailUsername)
            .NotEmpty()
            .WithMessage("Phone, Email or Username is required");
    }
}
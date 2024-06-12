using FluentValidation;
using XFramework.Blazor.Entity.Models.Requests.Session;

namespace XFramework.Blazor.Entity.Validations.Session;

public class CreateNewPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public CreateNewPasswordValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.PasswordConfirmation)
            .NotEmpty()
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match");
    }
}
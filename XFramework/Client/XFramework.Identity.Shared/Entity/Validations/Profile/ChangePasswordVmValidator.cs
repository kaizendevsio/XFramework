using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.Profile;

namespace XFramework.Identity.Shared.Entity.Validations.Profile
{
    public class ChangePasswordVmValidator: AbstractValidator<ChangePasswordVm>
    {
        public ChangePasswordVmValidator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty()
                .WithMessage("Enter your current password");
            RuleFor(x => x.NewPassword).NotEmpty()
                .WithMessage("Create a new password");
            RuleFor(x => x.VerifyPassword).NotEmpty()
                .WithMessage("Verify new password");
        }
    }
}
using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.User;
using XFramework.Identity.Shared.Entity.ViewModels.User.Session;

namespace XFramework.Identity.Shared.Entity.Validations.Session
{
    public class ForgotPasswordVmValidator : AbstractValidator<ForgotPasswordVm>
    {
        public ForgotPasswordVmValidator()
        {
            RuleFor(x => x.ContactNumber).NotEmpty()
                .WithMessage("Enter your contact number (+639 format)");
        }
    }
}
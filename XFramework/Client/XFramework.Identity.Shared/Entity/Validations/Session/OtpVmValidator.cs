using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.User;
using XFramework.Identity.Shared.Entity.ViewModels.User.Session;

namespace XFramework.Identity.Shared.Entity.Validations.Session
{
    public class OtpVmValidator : AbstractValidator<OtpVm>
    {
        public OtpVmValidator()
        {
            RuleFor(x => x.OtpCode).NotEmpty()
                .WithMessage("Enter the OTP code sent to your contact number");
        }
    }
}
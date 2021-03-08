using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.Profile;

namespace XFramework.Identity.Shared.Entity.Validations.Profile
{
    public class VerificationsVmValidator: AbstractValidator<VerificationsVm>
    {
        public VerificationsVmValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty()
                .WithMessage("Enter your first name");
            RuleFor(x => x.LastName).NotEmpty()
                .WithMessage("Enter your last name");
            RuleFor(x => x.MiddleInitial).NotEmpty()
                .WithMessage("Enter your middle initial");
            RuleFor(x => x.RegisteredContactNumber).NotEmpty()
                .WithMessage("Enter your contact number");
            RuleFor(x => x.EmailAddress).NotEmpty()
                .WithMessage("Enter your email address");
            RuleFor(x => x.BusinessType).NotEmpty()
                .WithMessage("Enter business type");
            RuleFor(x => x.BusinessAddress).NotEmpty()
                .WithMessage("Enter business address");
            /*RuleFor(x => x.City).NotEmpty()
            RuleFor(x => x.Province).NotEmpty()
            RuleFor(x => x.Region).NotEmpty()*/
            /*RuleFor(x => x.Video).NotEmpty()
            RuleFor(x => x.StoreFrontPhoto).NotEmpty()
            RuleFor(x => x.ValidIDPhoto).NotEmpty()
            RuleFor(x => x.SelfTakenPhoto).NotEmpty()
            RuleFor(x => x.IdTypes).NotEmpty()
            RuleFor(x => x.BusinessPermit).NotEmpty()
            RuleFor(x => x.TermsAndConditions).NotEmpty()*/
        }
        
    }
}
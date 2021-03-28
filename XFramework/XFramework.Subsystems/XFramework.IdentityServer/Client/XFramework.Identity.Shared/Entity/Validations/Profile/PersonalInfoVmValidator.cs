using FluentValidation;
using XFramework.Identity.Shared.Entity.ViewModels.Profile;

namespace XFramework.Identity.Shared.Entity.Validations.Profile
{
    public class PersonalInfoVmValidator: AbstractValidator<PersonalInfoVm>
    {
        public PersonalInfoVmValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty()
                .WithMessage("Enter your first name");
            RuleFor(x => x.LastName).NotEmpty()
                .WithMessage("Enter your last name");
            RuleFor(x => x.ContactNumber).NotEmpty()
                .WithMessage("Enter your contact number");
            RuleFor(x => x.EmailAddress).NotEmpty()
                .WithMessage("Enter your email address");

        }
    }
}
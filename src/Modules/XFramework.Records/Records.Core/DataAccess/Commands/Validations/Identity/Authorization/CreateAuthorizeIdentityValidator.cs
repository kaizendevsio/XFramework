using FluentValidation;
using Records.Core.DataAccess.Commands.Entity.Identity.Authorization;

namespace Records.Core.DataAccess.Commands.Validations.Identity.Authorization
{
    public class CreateAuthorizeIdentityValidator : CommandBaseValidator<CreateAuthorizeIdentityCmd>
    {
        public CreateAuthorizeIdentityValidator()
        {
            RuleFor(x => x.RequestServer).SetValidator(RequestServerValidator);

            RuleFor(x => x.UserName)
                .NotEmpty()
                .WithMessage("Username is Required");

            RuleFor(x => x.PasswordString)
                .NotEmpty()
                .WithMessage("Password is Required");

            RuleFor(x => x.Uid)
                .NotEmpty()
                .WithMessage("UUID is Required");
        }

    }
}
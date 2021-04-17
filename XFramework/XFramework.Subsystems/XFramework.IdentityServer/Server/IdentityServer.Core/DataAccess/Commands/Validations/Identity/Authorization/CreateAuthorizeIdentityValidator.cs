using FluentValidation;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;

namespace IdentityServer.Core.DataAccess.Commands.Validations.Identity.Authorization
{
    public class CreateAuthorizeIdentityValidator : CommandBaseValidator<CreateCredentialCmd>
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
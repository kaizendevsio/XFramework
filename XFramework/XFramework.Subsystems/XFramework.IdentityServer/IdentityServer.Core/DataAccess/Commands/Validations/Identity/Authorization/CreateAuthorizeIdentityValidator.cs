using FluentValidation;

namespace IdentityServer.Core.DataAccess.Commands.Validations.Identity.Authorization;

public class CreateAuthorizeIdentityValidator : CommandBaseValidator<CreateCredentialCmd>
{
    public CreateAuthorizeIdentityValidator()
    {
        RuleFor(x => x.RequestServer).SetValidator(RequestServerValidator);
            
        /*RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("Username is Required");*/
            
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is Required");
            
        RuleFor(x => x.Guid)
            .NotEmpty()
            .WithMessage("Guid is Required");
    }
        
}
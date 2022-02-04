using FluentValidation;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;

namespace IdentityServer.Core.DataAccess.Query.Validations.Identity.Authorization;

public class AuthenticateIdentityValidator : QueryBaseValidator<AuthenticateCredentialQuery>
{
    public AuthenticateIdentityValidator()
    {
        RuleFor(x => x.RequestServer).SetValidator(RequestServerValidator);

        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username is Required");
            
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is Required");
    }
}
using FluentValidation;
using RBS.Core.DataAccess.Query.Entity.Identity.Authorization;

namespace RBS.Core.DataAccess.Query.Validations.Identity.Authorization
{
    public class AuthenticateIdentityValidator : QueryBaseValidator<AuthenticateIdentityQuery>
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
}
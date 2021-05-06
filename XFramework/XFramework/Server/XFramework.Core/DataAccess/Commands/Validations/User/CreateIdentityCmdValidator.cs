using FluentValidation;
using XFramework.Core.DataAccess.Commands.Entity.Identity;

namespace XFramework.Core.DataAccess.Commands.Validations.User
{
    public class CreateIdentityCmdValidator : AbstractValidator<CreateIdentityCmd>
    {
        public CreateIdentityCmdValidator()
        {
            RuleFor(x => x.RequestServer.ApplicationId)
                .NotEmpty();
            RuleFor(x => x.UserName)
                .NotEmpty();
            RuleFor(x => x.PasswordString)
                .NotEmpty();
            RuleFor(x => x.FirstName)
                .NotEmpty();
            RuleFor(x => x.LastName)
                .NotEmpty();
            
        }
    }
}

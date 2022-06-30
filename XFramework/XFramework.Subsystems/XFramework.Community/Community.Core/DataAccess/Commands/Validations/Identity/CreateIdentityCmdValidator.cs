using Community.Core.DataAccess.Commands.Entity.Identity;
using FluentValidation;

namespace Community.Core.DataAccess.Commands.Validations.Identity;

public class CreateIdentityCmdValidator : AbstractValidator<CreateIdentityCmd>
{
    public CreateIdentityCmdValidator()
    {
        RuleFor(i => i.CredentialGuid)
            .NotEmpty();
        RuleFor(i => i.CommunityEntityGuid)
            .NotEmpty();
    }
}
using Community.Core.DataAccess.Commands.Entity.Identity;
using FluentValidation;

namespace Community.Core.DataAccess.Commands.Validations.Identity;

public class UpdateIdentityCmdValidator : AbstractValidator<UpdateIdentityCmd>
{
    public UpdateIdentityCmdValidator()
    {
        RuleFor(i => i.CredentialGuid)
            .NotEmpty();
        RuleFor(i => i.CommunityIdentityEntityGuid)
            .NotEmpty();
        RuleFor(i => i.Guid)
            .NotEmpty();
    }
}
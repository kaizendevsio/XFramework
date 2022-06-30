using Community.Core.DataAccess.Commands.Entity.Content;
using FluentValidation;

namespace Community.Core.DataAccess.Commands.Validations.Content;

public class UpdateContentCmdValidator : AbstractValidator<UpdateContentCmd>
{
    public UpdateContentCmdValidator()
    {
        RuleFor(i => i.Text)
            .NotEmpty();
        RuleFor(i => i.Guid)
            .NotNull();
        RuleFor(i => i.SocialMediaIdentityGuid)
            .NotNull();
    }
}
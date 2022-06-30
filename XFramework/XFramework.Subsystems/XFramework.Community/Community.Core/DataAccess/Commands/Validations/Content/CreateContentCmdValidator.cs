using Community.Core.DataAccess.Commands.Entity.Content;
using FluentValidation;

namespace Community.Core.DataAccess.Commands.Validations.Content;

public class CreateContentCmdValidator : AbstractValidator<CreateContentCmd>
{
    public CreateContentCmdValidator()
    {
        RuleFor(i => i.Text)
            .NotEmpty();
        RuleFor(i => i.ContentEntityGuid)
            .NotNull();
        RuleFor(i => i.CommunityIdentityGuid)
            .NotNull();
    }
}
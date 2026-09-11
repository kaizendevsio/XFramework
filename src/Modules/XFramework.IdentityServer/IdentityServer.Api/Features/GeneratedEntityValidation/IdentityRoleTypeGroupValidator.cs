using FluentValidation;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public sealed class IdentityRoleTypeGroupValidator : AbstractValidator<IdentityRoleTypeGroup>
{
    public IdentityRoleTypeGroupValidator() => RuleFor(x => x.Name).NotEmpty();
}

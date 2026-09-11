using FluentValidation;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public sealed class IdentityContactGroupValidator : AbstractValidator<IdentityContactGroup>
{
    public IdentityContactGroupValidator() => RuleFor(x => x.Name).NotEmpty();
}

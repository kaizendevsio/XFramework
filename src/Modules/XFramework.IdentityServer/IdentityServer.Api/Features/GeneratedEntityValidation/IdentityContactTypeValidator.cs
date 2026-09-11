using FluentValidation;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public sealed class IdentityContactTypeValidator : AbstractValidator<IdentityContactType>
{
    public IdentityContactTypeValidator() => RuleFor(x => x.Name).NotEmpty();
}

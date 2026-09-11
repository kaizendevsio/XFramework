using FluentValidation;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public sealed class SessionTypeValidator : AbstractValidator<SessionType>
{
    public SessionTypeValidator() => RuleFor(x => x.Name).NotEmpty();
}

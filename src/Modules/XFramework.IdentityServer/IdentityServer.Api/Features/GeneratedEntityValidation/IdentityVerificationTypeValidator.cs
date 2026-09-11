using FluentValidation;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public sealed class IdentityVerificationTypeValidator : AbstractValidator<IdentityVerificationType>
{
    public IdentityVerificationTypeValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}

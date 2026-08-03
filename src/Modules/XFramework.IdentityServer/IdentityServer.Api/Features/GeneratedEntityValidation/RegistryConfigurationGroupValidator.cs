using FluentValidation;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

public sealed class RegistryConfigurationGroupValidator : AbstractValidator<RegistryConfigurationGroup>
{
    public RegistryConfigurationGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.SystemReferenceId).NotEmpty();
    }
}

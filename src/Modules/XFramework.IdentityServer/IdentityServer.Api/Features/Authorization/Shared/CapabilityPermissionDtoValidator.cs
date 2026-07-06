using FluentValidation;

namespace IdentityServer.Api.Features.Authorization;

public sealed class CapabilityPermissionDtoValidator : AbstractValidator<CapabilityPermissionDto>
{
    public CapabilityPermissionDtoValidator()
    {
        RuleFor(x => x.ModuleKey)
            .NotEmpty().WithMessage("Module key is required");

        RuleFor(x => x.CapabilityKey)
            .NotEmpty().WithMessage("Capability key is required")
            .Must(BeKnownCapability).WithMessage("Capability key is invalid");

        RuleFor(x => x.Effect)
            .IsInEnum().WithMessage("Permission effect is invalid");
    }

    private static bool BeKnownCapability(string? value) =>
        IdentityAuthorizationConstants.CapabilityKeys.Contains(value?.Trim().ToLowerInvariant());
}

using FluentValidation;

namespace IdentityServer.Api.Features.Authorization;

public sealed class CapabilityPermissionDtoValidator : AbstractValidator<CapabilityPermissionDto>
{
    public CapabilityPermissionDtoValidator()
    {
        RuleFor(x => x.ModuleKey)
            .NotEmpty().WithMessage("Module key is required")
            .MaximumLength(128).WithMessage("Module key must not exceed 128 characters");

        RuleFor(x => x.SubFeatureKey)
            .MaximumLength(128).WithMessage("Sub-feature key must not exceed 128 characters");

        RuleFor(x => x.CapabilityKey)
            .NotEmpty().WithMessage("Capability key is required")
            .MaximumLength(64).WithMessage("Capability key must not exceed 64 characters")
            .Must(BeKnownCapability).WithMessage("Capability key is invalid");

        RuleFor(x => x.Effect)
            .IsInEnum().WithMessage("Permission effect is invalid");
    }

    private static bool BeKnownCapability(string? value) =>
        IdentityAuthorizationConstants.CapabilityKeys.Contains(value?.Trim().ToLowerInvariant());
}

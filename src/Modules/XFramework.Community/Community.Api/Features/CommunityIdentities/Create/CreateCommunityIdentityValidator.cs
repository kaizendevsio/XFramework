namespace Community.Api.Features.CommunityIdentities.Create;

/// <summary>
/// Validator for CreateCommunityIdentityRequest
/// </summary>
public sealed class CreateCommunityIdentityValidator : AbstractValidator<CreateCommunityIdentityRequest>
{
    public CreateCommunityIdentityValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.CommunityIdentityTypeId)
            .NotEmpty().WithMessage("Community Identity Type ID is required");

        RuleFor(x => x.HandleName)
            .MaximumLength(100).WithMessage("Handle name cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.HandleName));

        RuleFor(x => x.Tagline)
            .MaximumLength(200).WithMessage("Tagline cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Tagline));

        RuleFor(x => x.Alias)
            .MaximumLength(50).WithMessage("Alias cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Alias));
    }
}
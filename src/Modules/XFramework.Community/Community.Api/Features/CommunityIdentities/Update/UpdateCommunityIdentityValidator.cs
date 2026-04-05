namespace Community.Api.Features.CommunityIdentities.Update;

/// <summary>
/// Validator for UpdateCommunityIdentityRequest
/// </summary>
public sealed class UpdateCommunityIdentityValidator : AbstractValidator<UpdateCommunityIdentityRequest>
{
    public UpdateCommunityIdentityValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Community Identity ID is required");

        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required")
            .When(x => x.CredentialId != Guid.Empty);

        RuleFor(x => x.CommunityIdentityTypeId)
            .NotEmpty().WithMessage("Community Identity Type ID is required")
            .When(x => x.CommunityIdentityTypeId != Guid.Empty);
    }
}
namespace Community.Api.Features.CommunityIdentities.Get;

public sealed class GetCommunityIdentityValidator : AbstractValidator<GetCommunityIdentityRequest>
{
    public GetCommunityIdentityValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Community Identity ID is required");
    }
}

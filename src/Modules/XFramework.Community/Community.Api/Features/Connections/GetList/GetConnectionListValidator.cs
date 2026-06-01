namespace Community.Api.Features.Connections.GetList;

public sealed class GetConnectionListValidator : AbstractValidator<GetCommunityConnectionListRequest>
{
    public GetConnectionListValidator()
    {
        RuleFor(x => x.ConnectionTypeId)
            .NotEmpty().WithMessage("Connection Type ID is required");

        RuleFor(x => x.CommunityIdentityId)
            .NotEmpty().WithMessage("Community Identity ID is required");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100).WithMessage("Limit must be between 1 and 100");
    }
}

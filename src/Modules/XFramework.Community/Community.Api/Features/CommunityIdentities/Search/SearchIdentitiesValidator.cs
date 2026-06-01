namespace Community.Api.Features.CommunityIdentities.Search;

public sealed class SearchIdentitiesValidator : AbstractValidator<SearchIdentitiesRequest>
{
    public SearchIdentitiesValidator()
    {
        RuleFor(x => x.SearchTerm)
            .MaximumLength(100).WithMessage("Search term cannot exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.SearchTerm));

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Page index must be 1 or greater");
    }
}

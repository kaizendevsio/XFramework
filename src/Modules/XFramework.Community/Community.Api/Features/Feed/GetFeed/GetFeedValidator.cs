using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Feed.GetFeed;

/// <summary>
/// Validator for GetFeedRequest
/// </summary>
public sealed class GetFeedValidator : AbstractValidator<GetFeedRequest>
{
    public GetFeedValidator()
    {
        RuleFor(x => x.IdentityId)
            .NotEmpty().WithMessage("Identity ID is required");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Page index must be 0 or greater");
    }
}

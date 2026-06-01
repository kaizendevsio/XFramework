namespace Community.Api.Features.Notifications.GetList;

/// <summary>
/// Validator for GetNotificationsRequest
/// </summary>
public sealed class GetNotificationsValidator : AbstractValidator<GetNotificationsRequest>
{
    public GetNotificationsValidator()
    {
        RuleFor(x => x.IdentityId)
            .NotEmpty().WithMessage("Identity ID is required");

        RuleFor(x => x.RequestingIdentityId)
            .NotEmpty().WithMessage("Requesting Identity ID is required");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Page index must be 0 or greater");
    }
}

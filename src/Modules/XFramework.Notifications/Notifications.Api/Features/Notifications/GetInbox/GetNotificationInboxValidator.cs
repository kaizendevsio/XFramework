using FluentValidation;

namespace Notifications.Api.Features.Notifications.GetInbox;

public sealed class GetNotificationInboxValidator : AbstractValidator<GetNotificationInboxRequest>
{
    public GetNotificationInboxValidator()
    {
        RuleFor(x => x.RecipientCredentialId)
            .NotEmpty().WithMessage("Recipient credential ID is required");

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than zero");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100");
    }
}

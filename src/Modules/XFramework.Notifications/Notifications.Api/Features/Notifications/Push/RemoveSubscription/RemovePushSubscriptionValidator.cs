using FluentValidation;

namespace Notifications.Api.Features.Notifications.Push.RemoveSubscription;

public sealed class RemovePushSubscriptionValidator : AbstractValidator<RemovePushSubscriptionRequest>
{
    public RemovePushSubscriptionValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Push endpoint is required")
            .MaximumLength(2048).WithMessage("Push endpoint must not exceed 2048 characters");
    }
}

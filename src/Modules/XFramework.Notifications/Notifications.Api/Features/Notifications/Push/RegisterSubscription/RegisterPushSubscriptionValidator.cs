using FluentValidation;

namespace Notifications.Api.Features.Notifications.Push.RegisterSubscription;

public sealed class RegisterPushSubscriptionValidator : AbstractValidator<RegisterPushSubscriptionRequest>
{
    public RegisterPushSubscriptionValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Push endpoint is required")
            .MaximumLength(2048).WithMessage("Push endpoint must not exceed 2048 characters")
            .Must(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("Push endpoint must be an absolute https URL");

        RuleFor(x => x.P256dh)
            .NotEmpty().WithMessage("p256dh key is required")
            .MaximumLength(128).WithMessage("p256dh key must not exceed 128 characters");

        RuleFor(x => x.Auth)
            .NotEmpty().WithMessage("Auth secret is required")
            .MaximumLength(64).WithMessage("Auth secret must not exceed 64 characters");

        RuleFor(x => x.DeviceLabel)
            .MaximumLength(64).WithMessage("Device label must not exceed 64 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.DeviceLabel));
    }
}

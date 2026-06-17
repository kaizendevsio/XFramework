using FluentValidation;

namespace Notifications.Api.Features.Notifications.RecordDeliveryStatus;

public sealed class RecordNotificationDeliveryStatusValidator : AbstractValidator<RecordNotificationDeliveryStatusRequest>
{
    public RecordNotificationDeliveryStatusValidator()
    {
        RuleFor(x => x.NotificationInboxItemId)
            .NotEmpty().WithMessage("Notification inbox item ID is required");

        RuleFor(x => x.Channel)
            .Must(channel => channel is not NotificationDeliveryChannel.None &&
                             (channel & ~NotificationDeliveryChannel.All) == 0 &&
                             IsSingleChannel(channel))
            .WithMessage("A single supported delivery channel is required");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Delivery status is invalid");

        RuleFor(x => x.ProviderMessageId)
            .MaximumLength(256).WithMessage("Provider message ID must not exceed 256 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ProviderMessageId));

        RuleFor(x => x.ErrorCode)
            .MaximumLength(128).WithMessage("Error code must not exceed 128 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ErrorCode));

        RuleFor(x => x.ErrorMessage)
            .MaximumLength(2000).WithMessage("Error message must not exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ErrorMessage));

        RuleFor(x => x.AttemptNumber)
            .GreaterThanOrEqualTo(0).WithMessage("Attempt number cannot be negative");
    }

    private static bool IsSingleChannel(NotificationDeliveryChannel channel)
    {
        var value = (int)channel;
        return (value & (value - 1)) == 0;
    }
}

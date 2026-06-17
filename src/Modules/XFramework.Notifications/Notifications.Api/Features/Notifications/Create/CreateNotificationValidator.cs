using FluentValidation;

namespace Notifications.Api.Features.Notifications.Create;

public sealed class CreateNotificationValidator : AbstractValidator<CreateNotificationRequest>
{
    public CreateNotificationValidator()
    {
        RuleFor(x => x.RecipientCredentialId)
            .NotEmpty().WithMessage("Recipient credential ID is required");

        RuleFor(x => x.TemplateKey)
            .NotEmpty().WithMessage("Template key is required")
            .MaximumLength(128).WithMessage("Template key must not exceed 128 characters");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(256).WithMessage("Title must not exceed 256 characters");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required")
            .MaximumLength(4000).WithMessage("Body must not exceed 4000 characters");

        RuleFor(x => x.DeliveryChannels)
            .Must(channels => channels != NotificationDeliveryChannel.None)
            .WithMessage("At least one delivery channel is required")
            .Must(channels => (channels & ~NotificationDeliveryChannel.All) == 0)
            .WithMessage("Delivery channels contain an unsupported value");

        RuleFor(x => x.CorrelationId)
            .MaximumLength(128).WithMessage("Correlation ID must not exceed 128 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.CorrelationId));
    }
}

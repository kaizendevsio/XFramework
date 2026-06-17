using FluentValidation;

namespace Notifications.Api.Features.Notifications.MarkRead;

public sealed class MarkNotificationReadValidator : AbstractValidator<MarkNotificationReadRequest>
{
    public MarkNotificationReadValidator()
    {
        RuleFor(x => x.RecipientCredentialId)
            .NotEmpty().WithMessage("Recipient credential ID is required");

        RuleFor(x => x.NotificationIds)
            .NotEmpty().WithMessage("At least one notification ID is required")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Notification IDs must be unique");

        RuleForEach(x => x.NotificationIds)
            .NotEmpty().WithMessage("Notification ID cannot be empty");
    }
}

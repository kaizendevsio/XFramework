using FluentValidation;
using Community.Domain.Shared.Contracts.Requests;

namespace Community.Api.Features.Notifications.MarkRead;

/// <summary>
/// Validator for MarkNotificationsReadRequest
/// </summary>
public sealed class MarkNotificationsReadValidator : AbstractValidator<MarkNotificationsReadRequest>
{
    public MarkNotificationsReadValidator()
    {
        RuleFor(x => x.NotificationIds)
            .NotEmpty().WithMessage("At least one notification ID is required");

        RuleForEach(x => x.NotificationIds)
            .NotEmpty().WithMessage("Notification ID must not be empty");
    }
}

using FluentValidation;

namespace Notifications.Api.Features.Notifications.UpdatePreferences;

public sealed class UpdateNotificationPreferencesValidator : AbstractValidator<UpdateNotificationPreferencesRequest>
{
    public UpdateNotificationPreferencesValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.EnabledChannels)
            .Must(channels => (channels & ~NotificationDeliveryChannel.All) == 0)
            .WithMessage("Enabled channels contain an unsupported value");

        RuleForEach(x => x.DisabledTemplateKeys)
            .NotEmpty().WithMessage("Disabled template key cannot be empty")
            .MaximumLength(128).WithMessage("Disabled template key must not exceed 128 characters");
    }
}

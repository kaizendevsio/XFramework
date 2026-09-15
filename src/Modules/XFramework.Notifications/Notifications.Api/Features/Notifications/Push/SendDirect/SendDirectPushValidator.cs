using FluentValidation;

namespace Notifications.Api.Features.Notifications.Push.SendDirect;

public sealed class SendDirectPushValidator : AbstractValidator<SendDirectPushRequest>
{
    public SendDirectPushValidator()
    {
        RuleFor(x => x.RecipientCredentialId)
            .NotEmpty().WithMessage("Recipient credential ID is required");

        RuleFor(x => x.Kind)
            .NotEmpty().WithMessage("Push kind is required")
            .MaximumLength(32).WithMessage("Push kind must not exceed 32 characters");

        RuleFor(x => x.Reference)
            .MaximumLength(64).WithMessage("Reference must not exceed 64 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Reference));

        RuleFor(x => x.TimeToLiveSeconds)
            .InclusiveBetween(0, 2419200).WithMessage("Time to live must be between 0 and 2419200 seconds");
    }
}

using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Create;

namespace Messaging.Api.Features.Messages.CreateDirect;

/// <summary>
/// Validator for CreateDirectMessageRequest
/// </summary>
public sealed class CreateDirectMessageValidator : AbstractValidator<CreateDirectMessageRequest>
{
    public CreateDirectMessageValidator()
    {
        RuleFor(x => x.Recipient)
            .NotEmpty().WithMessage("Recipient is required")
            .MaximumLength(100).WithMessage("Recipient cannot exceed 100 characters");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(1000).WithMessage("Message cannot exceed 1000 characters");

        RuleFor(x => x.MessageTransportType)
            .IsInEnum().WithMessage("Invalid message transport type");

        RuleFor(x => x.Sender)
            .MaximumLength(100).WithMessage("Sender cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Sender));

        RuleFor(x => x.Subject)
            .MaximumLength(200).WithMessage("Subject cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Subject));

        RuleFor(x => x.Intent)
            .MaximumLength(100).WithMessage("Intent cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Intent));
    }
}
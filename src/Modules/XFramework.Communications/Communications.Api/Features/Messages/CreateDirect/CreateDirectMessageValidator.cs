using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Create;

namespace Communications.Api.Features.Messages.CreateDirect;

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
            .MaximumLength(4000).WithMessage("Message cannot exceed 4000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Message));

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Message) || x.TemplateId is not null || !string.IsNullOrWhiteSpace(x.TemplateKey))
            .WithMessage("Message text or template is required");

        RuleFor(x => x.TemplateKey)
            .MaximumLength(128).WithMessage("Template key cannot exceed 128 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.TemplateKey));

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

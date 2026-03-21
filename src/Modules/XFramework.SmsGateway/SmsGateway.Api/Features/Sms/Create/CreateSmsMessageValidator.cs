using FluentValidation;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;

namespace SmsGateway.Api.Features.Sms.Create;

/// <summary>
/// Validator for CreateSmsMessageRequest
/// </summary>
public class CreateSmsMessageValidator : AbstractValidator<CreateSmsMessageRequest>
{
    public CreateSmsMessageValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.AgentClusterId)
            .NotEmpty().WithMessage("Agent cluster ID is required");

        RuleFor(x => x.Recipient)
            .NotEmpty().WithMessage("Recipient is required")
            .MaximumLength(20).WithMessage("Recipient cannot exceed 20 characters");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(160).WithMessage("Message cannot exceed 160 characters");

        RuleFor(x => x.Sender)
            .MaximumLength(20).WithMessage("Sender cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.Sender));

        RuleFor(x => x.Subject)
            .MaximumLength(100).WithMessage("Subject cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Subject));
    }
}
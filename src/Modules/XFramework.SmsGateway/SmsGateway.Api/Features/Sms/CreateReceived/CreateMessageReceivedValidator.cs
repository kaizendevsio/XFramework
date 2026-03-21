using FluentValidation;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;

namespace SmsGateway.Api.Features.Sms.CreateReceived;

/// <summary>
/// Validator for CreateMessageReceivedRequest
/// </summary>
public class CreateMessageReceivedValidator : AbstractValidator<CreateMessageReceivedRequest>
{
    public CreateMessageReceivedValidator()
    {
        RuleFor(x => x.AgentClusterId)
            .NotEmpty().WithMessage("Agent cluster ID is required");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(500).WithMessage("Message cannot exceed 500 characters");

        RuleFor(x => x.Sender)
            .MaximumLength(20).WithMessage("Sender cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.Sender));

        RuleFor(x => x.SubscriptionId)
            .MaximumLength(50).WithMessage("Subscription ID cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.SubscriptionId));

        RuleFor(x => x.ReceivedAt)
            .Must(BeAValidDateTime).WithMessage("ReceivedAt must be a valid date time string")
            .When(x => !string.IsNullOrEmpty(x.ReceivedAt));
    }

    private bool BeAValidDateTime(string? dateTimeString)
    {
        if (string.IsNullOrEmpty(dateTimeString))
            return true;
        
        return DateTime.TryParse(dateTimeString, out _);
    }
}
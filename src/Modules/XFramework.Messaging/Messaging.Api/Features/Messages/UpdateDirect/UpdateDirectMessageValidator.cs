using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Update;

namespace Messaging.Api.Features.Messages.UpdateDirect;

/// <summary>
/// Validator for UpdateMessageDirectRequest
/// </summary>
public sealed class UpdateDirectMessageValidator : AbstractValidator<UpdateMessageDirectRequest>
{
    public UpdateDirectMessageValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.AgentClusterId)
            .NotEmpty().WithMessage("Agent Cluster ID is required");

        RuleFor(x => x.SentAt)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("SentAt cannot be in the future")
            .When(x => x.SentAt.HasValue);

        RuleFor(x => x.ReceivedAt)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("ReceivedAt cannot be in the future")
            .When(x => x.ReceivedAt.HasValue);

        RuleFor(x => x.ReceivedAt)
            .GreaterThanOrEqualTo(x => x.SentAt).WithMessage("ReceivedAt must be after SentAt")
            .When(x => x.SentAt.HasValue && x.ReceivedAt.HasValue);
    }
}
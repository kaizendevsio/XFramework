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

        RuleFor(x => x.RecievedAt)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("RecievedAt cannot be in the future")
            .When(x => x.RecievedAt.HasValue);

        RuleFor(x => x.RecievedAt)
            .GreaterThanOrEqualTo(x => x.SentAt).WithMessage("RecievedAt must be after SentAt")
            .When(x => x.SentAt.HasValue && x.RecievedAt.HasValue);
    }
}
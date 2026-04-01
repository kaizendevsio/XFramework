using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Threads;

namespace Messaging.Api.Features.Messages.MarkRead;

public sealed class MarkMessagesReadValidator : AbstractValidator<MarkMessagesReadRequest>
{
    public MarkMessagesReadValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.RequesterCredentialId)
            .NotEmpty().WithMessage("Requester Credential ID is required");

        RuleFor(x => x.MessageIds)
            .NotEmpty().WithMessage("At least one message ID is required");

        RuleForEach(x => x.MessageIds)
            .NotEmpty().WithMessage("Message ID cannot be empty");
    }
}

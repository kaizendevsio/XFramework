using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Threads;

namespace Communications.Api.Features.Messages.MarkRead;

public sealed class MarkMessagesReadValidator : AbstractValidator<MarkMessagesReadRequest>
{
    public MarkMessagesReadValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageIds)
            .NotEmpty().WithMessage("At least one message ID is required")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Message IDs must be unique");

        RuleForEach(x => x.MessageIds)
            .NotEmpty().WithMessage("Message ID cannot be empty");
    }
}

using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;

namespace Messaging.Api.Features.Messages.Reactions.Delete;

public sealed class DeleteMessageReactionValidator : AbstractValidator<DeleteMessageReactionRequest>
{
    public DeleteMessageReactionValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.ReactionId)
            .NotEmpty().WithMessage("Reaction ID is required");
    }
}

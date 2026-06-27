using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Reactions;

namespace Communications.Api.Features.Messages.Reactions.Create;

public sealed class CreateMessageReactionValidator : AbstractValidator<CreateMessageReactionRequest>
{
    public CreateMessageReactionValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.TypeId)
            .NotEmpty().WithMessage("Reaction Type ID is required");
    }
}

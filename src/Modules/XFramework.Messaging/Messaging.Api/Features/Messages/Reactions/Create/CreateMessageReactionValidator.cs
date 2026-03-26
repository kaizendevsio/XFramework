using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;

namespace Messaging.Api.Features.Messages.Reactions.Create;

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

        RuleFor(x => x.RequesterCredentialId)
            .NotEmpty().WithMessage("Requester Credential ID is required");
    }
}

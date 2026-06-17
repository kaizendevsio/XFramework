using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Delete;

namespace Messaging.Api.Features.Messages.DeleteMessage;

public sealed class DeleteThreadMessageValidator : AbstractValidator<DeleteThreadMessageRequest>
{
    public DeleteThreadMessageValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required");
    }
}

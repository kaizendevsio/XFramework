using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Threads;

namespace Messaging.Api.Features.Messages.CreateMessage;

public sealed class CreateThreadMessageValidator : AbstractValidator<CreateThreadMessageRequest>
{
    public CreateThreadMessageValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.SenderCredentialId)
            .NotEmpty().WithMessage("Sender credential ID is required");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Message text is required")
            .MaximumLength(5000).WithMessage("Message text cannot exceed 5000 characters");
    }
}

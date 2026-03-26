using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Edit;

namespace Messaging.Api.Features.Messages.EditMessage;

public sealed class EditThreadMessageValidator : AbstractValidator<EditThreadMessageRequest>
{
    public EditThreadMessageValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.RequesterCredentialId)
            .NotEmpty().WithMessage("Requester Credential ID is required");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required")
            .MaximumLength(5000).WithMessage("Text cannot exceed 5000 characters");
    }
}

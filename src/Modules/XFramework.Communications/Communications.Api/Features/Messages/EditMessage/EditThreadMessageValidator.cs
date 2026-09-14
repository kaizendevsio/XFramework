using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Edit;

namespace Communications.Api.Features.Messages.EditMessage;

public sealed class EditThreadMessageValidator : AbstractValidator<EditThreadMessageRequest>
{
    public EditThreadMessageValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required")
            .MaximumLength(5000).WithMessage("Text cannot exceed 5000 characters");

        RuleFor(x => x).Must(x => Communications.Domain.Shared.Contracts.EncryptedMessages.ValidEnvelope(x.EncryptedEnvelope)
            && x.Text == Communications.Domain.Shared.Contracts.EncryptedMessages.Preview)
            .When(x => x.EncryptedEnvelope is not null)
            .WithMessage("Encrypted edits must not contain plaintext.");
    }
}

using FluentValidation;
using Communications.Domain.Shared.Contracts.Requests.Attachments;

namespace Communications.Api.Features.Messages.Attachments.Delete;

public sealed class DeleteMessageFileValidator : AbstractValidator<DeleteMessageFileRequest>
{
    public DeleteMessageFileValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required");
    }
}

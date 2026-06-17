using FluentValidation;
using Messaging.Domain.Shared.Contracts.Requests.Attachments;

namespace Messaging.Api.Features.Messages.Attachments.Create;

public sealed class CreateMessageFileValidator : AbstractValidator<CreateMessageFileRequest>
{
    public CreateMessageFileValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty().WithMessage("Thread ID is required");

        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("Message ID is required");

        RuleFor(x => x.StorageFileId)
            .NotEmpty().WithMessage("Storage File ID is required");
    }
}

using FluentValidation;

namespace Storage.Api.Features.ChatUploads.ValidateReference;

public sealed class ValidateChatStorageFileReferenceValidator : AbstractValidator<ValidateChatStorageFileReferenceRequest>
{
    public ValidateChatStorageFileReferenceValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.StorageFileId).NotEmpty();
    }
}

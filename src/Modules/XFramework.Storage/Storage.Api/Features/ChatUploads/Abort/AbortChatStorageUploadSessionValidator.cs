using FluentValidation;

namespace Storage.Api.Features.ChatUploads.Abort;

public sealed class AbortChatStorageUploadSessionValidator : AbstractValidator<AbortChatStorageUploadSessionRequest>
{
    public AbortChatStorageUploadSessionValidator() => RuleFor(x => x.UploadSessionId).NotEmpty();
}

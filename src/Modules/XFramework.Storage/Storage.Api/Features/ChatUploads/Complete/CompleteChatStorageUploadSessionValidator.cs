using FluentValidation;
using Storage.Api.Validation;

namespace Storage.Api.Features.ChatUploads.Complete;

public sealed class CompleteChatStorageUploadSessionValidator : AbstractValidator<CompleteChatStorageUploadSessionRequest>
{
    public CompleteChatStorageUploadSessionValidator()
    {
        RuleFor(x => x.UploadSessionId).NotEmpty();
        RuleFor(x => x.ExpectedSha256Hash).OptionalSha256();
    }
}

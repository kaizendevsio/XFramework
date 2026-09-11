using FluentValidation;
using Storage.Api.Validation;

namespace Storage.Api.Features.ChatUploads.UploadPart;

public sealed class UploadChatStorageFilePartValidator : AbstractValidator<UploadChatStorageFilePartRequest>
{
    public UploadChatStorageFilePartValidator()
    {
        RuleFor(x => x.UploadSessionId).NotEmpty();
        RuleFor(x => x.PartNumber).GreaterThan(0);
        RuleFor(x => x.OffsetBytes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PartSha256Hash).NotEmpty().OptionalSha256();
        RuleFor(x => x.ChunkBytes).NotEmpty().Must(bytes => bytes is { Length: <= 100 * 1024 * 1024 });
    }
}

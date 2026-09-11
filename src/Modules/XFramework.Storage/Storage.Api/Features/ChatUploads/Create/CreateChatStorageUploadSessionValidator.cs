using FluentValidation;
using Microsoft.Extensions.Options;
using Storage.Api.Validation;

namespace Storage.Api.Features.ChatUploads.Create;

public sealed class CreateChatStorageUploadSessionValidator : AbstractValidator<CreateChatStorageUploadSessionRequest>
{
    public CreateChatStorageUploadSessionValidator(IOptions<StorageOptions> options)
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(255);
        RuleFor(x => x.TotalSizeBytes).GreaterThan(0).LessThanOrEqualTo(options.Value.MaxFileSizeBytes);
        RuleFor(x => x.ChunkSizeBytes).InclusiveBetween(1, 100 * 1024 * 1024).When(x => x.ChunkSizeBytes.HasValue);
        RuleFor(x => x.ExpectedSha256Hash).OptionalSha256();
    }
}

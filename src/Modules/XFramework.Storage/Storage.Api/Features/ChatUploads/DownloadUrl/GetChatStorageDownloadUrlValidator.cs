using FluentValidation;
using Microsoft.Extensions.Options;

namespace Storage.Api.Features.ChatUploads.DownloadUrl;

public sealed class GetChatStorageDownloadUrlValidator : AbstractValidator<GetChatStorageDownloadUrlRequest>
{
    public GetChatStorageDownloadUrlValidator(IOptions<StorageOptions> options)
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.StorageFileId).NotEmpty();
        RuleFor(x => x.ExpirationMinutes).InclusiveBetween(1, Math.Max(1, options.Value.MaxSignedUrlExpirationMinutes))
            .When(x => x.ExpirationMinutes.HasValue);
    }
}

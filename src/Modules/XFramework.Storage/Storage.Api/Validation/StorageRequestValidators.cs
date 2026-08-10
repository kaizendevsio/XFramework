using FluentValidation;
using Microsoft.Extensions.Options;

namespace Storage.Api.Validation;

internal static class StorageValidationRules
{
    public static IRuleBuilderOptions<T, string?> OptionalSha256<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => string.IsNullOrWhiteSpace(value) ||
                           value.Trim().Length == 64 && value.Trim().All(Uri.IsHexDigit))
            .WithMessage("SHA-256 hash must contain 64 hexadecimal characters");
}

public sealed class EnsureStorageUploadMetadataRequestValidator : AbstractValidator<EnsureStorageUploadMetadataRequest>
{
    public EnsureStorageUploadMetadataRequestValidator()
    {
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(255);
        RuleFor(x => x.IdentifierGroupName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdentifierName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdentifierDescription).MaximumLength(500);
    }
}

public sealed class CreateStorageUploadSessionRequestValidator : AbstractValidator<CreateStorageUploadSessionRequest>
{
    public CreateStorageUploadSessionRequestValidator(IOptions<StorageOptions> options)
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).MaximumLength(255);
        RuleFor(x => x.TypeId).NotEmpty();
        RuleFor(x => x.StorageFileIdentifierId).NotEmpty();
        RuleFor(x => x.TotalSizeBytes).GreaterThan(0).LessThanOrEqualTo(options.Value.MaxFileSizeBytes);
        RuleFor(x => x.ChunkSizeBytes).InclusiveBetween(1, 100 * 1024 * 1024).When(x => x.ChunkSizeBytes.HasValue);
        RuleFor(x => x.Visibility).IsInEnum();
        RuleFor(x => x.ProviderProfileName).MaximumLength(200);
        RuleFor(x => x.ExpectedSha256Hash).OptionalSha256();
    }
}

public sealed class UploadStorageFilePartRequestValidator : AbstractValidator<UploadStorageFilePartRequest>
{
    public UploadStorageFilePartRequestValidator()
    {
        RuleFor(x => x.UploadSessionId).NotEmpty();
        RuleFor(x => x.PartNumber).GreaterThan(0);
        RuleFor(x => x.OffsetBytes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PartSha256Hash).NotEmpty().OptionalSha256();
        RuleFor(x => x.ChunkBytes).NotEmpty().Must(bytes => bytes is { Length: <= 100 * 1024 * 1024 })
            .WithMessage("Upload part exceeds the 100 MB limit");
    }
}

public sealed class CompleteStorageUploadSessionRequestValidator : AbstractValidator<CompleteStorageUploadSessionRequest>
{
    public CompleteStorageUploadSessionRequestValidator()
    {
        RuleFor(x => x.UploadSessionId).NotEmpty();
        RuleFor(x => x.ExpectedSha256Hash).OptionalSha256();
    }
}

public sealed class AbortStorageUploadSessionRequestValidator : AbstractValidator<AbortStorageUploadSessionRequest>
{
    public AbortStorageUploadSessionRequestValidator() => RuleFor(x => x.UploadSessionId).NotEmpty();
}

public sealed class ListStorageUploadPartsRequestValidator : AbstractValidator<ListStorageUploadPartsRequest>
{
    public ListStorageUploadPartsRequestValidator() => RuleFor(x => x.UploadSessionId).NotEmpty();
}

public sealed class GetStorageFileRequestValidator : AbstractValidator<GetStorageFileRequest>
{
    public GetStorageFileRequestValidator() => RuleFor(x => x.StorageFileId).NotEmpty();
}

public sealed class GetStorageFilesRequestValidator : AbstractValidator<GetStorageFilesRequest>
{
    public GetStorageFilesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.SearchTerm).MaximumLength(255);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Visibility).IsInEnum().When(x => x.Visibility.HasValue);
    }
}

public sealed class GetStorageDownloadUrlRequestValidator : AbstractValidator<GetStorageDownloadUrlRequest>
{
    public GetStorageDownloadUrlRequestValidator(IOptions<StorageOptions> options)
    {
        RuleFor(x => x.StorageFileId).NotEmpty();
        RuleFor(x => x.ExpirationMinutes)
            .InclusiveBetween(1, Math.Max(1, options.Value.MaxSignedUrlExpirationMinutes))
            .When(x => x.ExpirationMinutes.HasValue);
    }
}

public sealed class GetStoragePublicUrlRequestValidator : AbstractValidator<GetStoragePublicUrlRequest>
{
    public GetStoragePublicUrlRequestValidator() => RuleFor(x => x.StorageFileId).NotEmpty();
}

public sealed class ValidateStorageFileReferenceRequestValidator : AbstractValidator<ValidateStorageFileReferenceRequest>
{
    public ValidateStorageFileReferenceRequestValidator() => RuleFor(x => x.StorageFileId).NotEmpty();
}

public sealed class ClaimStorageFileRequestValidator : AbstractValidator<ClaimStorageFileRequest>
{
    public ClaimStorageFileRequestValidator() => RuleFor(x => x.StorageFileId).NotEmpty();
}

public sealed class DeleteStorageFileRequestValidator : AbstractValidator<DeleteStorageFileRequest>
{
    public DeleteStorageFileRequestValidator() => RuleFor(x => x.StorageFileId).NotEmpty();
}

public sealed class RestoreStorageFileRequestValidator : AbstractValidator<RestoreStorageFileRequest>
{
    public RestoreStorageFileRequestValidator() => RuleFor(x => x.StorageFileId).NotEmpty();
}

public sealed class CleanupStorageRetentionRequestValidator : AbstractValidator<CleanupStorageRetentionRequest>
{
    public CleanupStorageRetentionRequestValidator() => RuleFor(x => x.MaxFiles).InclusiveBetween(1, 1000);
}

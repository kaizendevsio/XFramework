using FluentValidation;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Metadata.EnsureUpload;

public static class EnsureStorageUploadMetadataEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite])]
    [MapPost("/api/storage/metadata/upload", Tags = ["Storage"],
        Summary = "Ensure upload metadata",
        Description = "Ensures tenant-scoped file type and identifier metadata for a Storage upload.",
        RequireAuthorization = true)]
    public static Task<Result<StorageUploadMetadataResponse>> Handle(
        EnsureStorageUploadMetadataRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.EnsureUploadMetadataAsync(request, ct);
}

public sealed class EnsureStorageUploadMetadataRequestValidator
    : AbstractValidator<EnsureStorageUploadMetadataRequest>
{
    public EnsureStorageUploadMetadataRequestValidator()
    {
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(255);
        RuleFor(x => x.IdentifierGroupName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdentifierName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdentifierDescription).MaximumLength(500);
    }
}

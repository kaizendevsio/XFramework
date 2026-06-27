using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Files.ValidateReference;

public static class ValidateStorageFileReferenceEndpoint
{
    [BoltHandler]
    [MapPost("/api/storage/files/{storageFileId:guid}/validate-reference", Tags = ["Storage"],
        Summary = "Validate storage file reference",
        Description = "Validates tenant ownership and availability before another module references a file.")]
    public static Task<Result<StorageFileValidationResponse>> Handle(
        ValidateStorageFileReferenceRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.ValidateFileReferenceAsync(request, ct);
}

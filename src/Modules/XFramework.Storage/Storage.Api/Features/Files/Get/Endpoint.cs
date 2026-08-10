using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Files.Get;

public static class GetStorageFileMetadataEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageRead])]
    [MapGet("/api/storage/files/{storageFileId:guid}", Tags = ["Storage"],
        RequiredServiceScopes = [],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.View],
        Capability = StorageAuthorizationCapabilities.ViewKey,
        Summary = "Get storage file",
        Description = "Gets tenant-scoped storage file metadata.")]
    public static Task<Result<StorageFileResponse>> Handle(
        [AsParameters] GetStorageFileRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.GetFileAsync(request, ct);
}

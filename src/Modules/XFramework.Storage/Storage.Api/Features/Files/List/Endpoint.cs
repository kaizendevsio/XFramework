using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Files.List;

public static class GetStorageFilesEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageRead])]
    [MapGet("/api/storage/files", Tags = ["Storage"],
        Summary = "List storage files",
        Description = "Lists tenant-scoped storage file metadata.")]
    public static Task<Result<StorageFileListResponse>> Handle(
        [AsParameters] GetStorageFilesRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.GetFilesAsync(request, ct);
}

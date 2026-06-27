using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Files.Get;

public static class GetStorageFileEndpoint
{
    [BoltHandler]
    [MapGet("/api/storage/files/{storageFileId:guid}", Tags = ["Storage"],
        Summary = "Get storage file",
        Description = "Gets tenant-scoped storage file metadata.")]
    public static Task<Result<StorageFileResponse>> Handle(
        [AsParameters] GetStorageFileRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.GetFileAsync(request, ct);
}

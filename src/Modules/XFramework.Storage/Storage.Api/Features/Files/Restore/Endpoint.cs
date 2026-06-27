using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Files.Restore;

public static class RestoreStorageFileEndpoint
{
    [BoltHandler]
    [MapPost("/api/storage/files/{storageFileId:guid}/restore", Tags = ["Storage"],
        Summary = "Restore storage file",
        Description = "Restores soft-deleted storage metadata before retention cleanup deletes the object.")]
    public static Task<Result<StorageFileResponse>> Handle(
        RestoreStorageFileRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.RestoreFileAsync(request, ct);
}

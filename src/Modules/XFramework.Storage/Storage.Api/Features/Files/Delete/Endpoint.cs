using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Files.Delete;

public static class DeleteStorageFileEndpoint
{
    [BoltHandler]
    [MapDelete("/api/storage/files/{storageFileId:guid}", Tags = ["Storage"],
        Summary = "Delete storage file",
        Description = "Soft-deletes storage metadata and schedules physical deletion by retention cleanup.")]
    public static Task<Result> Handle(
        DeleteStorageFileRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.DeleteFileAsync(request, ct);
}

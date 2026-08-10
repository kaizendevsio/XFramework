using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Files.DownloadUrl;

public static class GetStorageDownloadUrlEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageRead])]
    [MapPost("/api/storage/files/{storageFileId:guid}/download-url", Tags = ["Storage"],
        RequiredServiceScopes = [],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.View],
        Capability = StorageAuthorizationCapabilities.ViewKey,
        Summary = "Create download URL",
        Description = "Returns a short-lived signed URL for private files or a public URL for public assets.")]
    public static Task<Result<StorageDownloadUrlResponse>> Handle(
        GetStorageDownloadUrlRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.GetDownloadUrlAsync(request, ct);
}

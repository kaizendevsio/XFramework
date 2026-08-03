using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Files.PublicUrl;

public static class GetStoragePublicUrlEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageRead])]
    [MapGet("/api/storage/files/{storageFileId:guid}/public-url", Tags = ["Storage"],
        Summary = "Get public URL",
        Description = "Returns the configured public/CDN URL for public storage assets.")]
    public static Task<Result<StoragePublicUrlResponse>> Handle(
        [AsParameters] GetStoragePublicUrlRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.GetPublicUrlAsync(request, ct);
}

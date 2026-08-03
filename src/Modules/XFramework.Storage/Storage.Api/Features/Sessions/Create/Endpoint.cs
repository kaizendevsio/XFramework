using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Sessions.Create;

public static class CreateStorageUploadSessionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite])]
    [MapPost("/api/storage/uploads/sessions", Tags = ["Storage"],
        Summary = "Create upload session",
        Description = "Creates tenant-scoped file metadata and a resumable upload session.")]
    public static Task<Result<StorageUploadSessionResponse>> Handle(
        CreateStorageUploadSessionRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.CreateUploadSessionAsync(request, ct);
}

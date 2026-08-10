using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Sessions.Complete;

public static class CompleteStorageUploadSessionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite])]
    [MapPost("/api/storage/uploads/sessions/{uploadSessionId:guid}/complete", Tags = ["Storage"],
        RequiredServiceScopes = [],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.Manage],
        Capability = StorageAuthorizationCapabilities.ManageKey,
        Summary = "Complete upload session",
        Description = "Completes provider multipart/block upload and marks the file available.")]
    public static Task<Result<StorageFileResponse>> Handle(
        CompleteStorageUploadSessionRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.CompleteUploadAsync(request, ct);
}

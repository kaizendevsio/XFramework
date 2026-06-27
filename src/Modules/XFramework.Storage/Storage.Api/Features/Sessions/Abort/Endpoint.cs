using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Sessions.Abort;

public static class AbortStorageUploadSessionEndpoint
{
    [BoltHandler]
    [MapPost("/api/storage/uploads/sessions/{uploadSessionId:guid}/abort", Tags = ["Storage"],
        Summary = "Abort upload session",
        Description = "Aborts a resumable upload session.")]
    public static Task<Result> Handle(
        AbortStorageUploadSessionRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.AbortUploadAsync(request, ct);
}

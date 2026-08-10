using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Sessions.ListParts;

public static class ListStorageUploadPartsEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageRead])]
    [MapGet("/api/storage/uploads/sessions/{uploadSessionId:guid}/parts", Tags = ["Storage"],
        RequiredServiceScopes = [],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.View],
        Capability = StorageAuthorizationCapabilities.ViewKey,
        Summary = "List upload parts",
        Description = "Lists uploaded and missing parts for a resumable upload session.")]
    public static Task<Result<StorageUploadPartListResponse>> Handle(
        [AsParameters] ListStorageUploadPartsRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.ListPartsAsync(request, ct);
}

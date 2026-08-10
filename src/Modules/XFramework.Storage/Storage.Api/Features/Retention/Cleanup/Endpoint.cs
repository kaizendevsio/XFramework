using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Retention.Cleanup;

public static class CleanupStorageRetentionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite])]
    [MapPost("/api/storage/retention/cleanup", Tags = ["Storage"],
        RequiredServiceScopes = [],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.Manage],
        Capability = StorageAuthorizationCapabilities.ManageKey,
        Summary = "Run retention cleanup",
        Description = "Physically deletes retained objects whose metadata was already soft-deleted.")]
    public static Task<Result<StorageRetentionCleanupResponse>> Handle(
        CleanupStorageRetentionRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.CleanupRetentionAsync(request, ct);
}

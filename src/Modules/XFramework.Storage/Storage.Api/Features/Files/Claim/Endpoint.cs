using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Storage.Api.Features.Files.Claim;

public static class ClaimStorageFileEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.IdentityServer, XFrameworkServiceNames.Portal])]
    public static Task<Result<StorageFileResponse>> Handle(
        ClaimStorageFileRequest request, StorageService storageService, CancellationToken ct) =>
        storageService.ClaimFileAsync(request, ct);

    // Keep HTTP actor requirements separate from the trusted service RPC policy.
    [MapPost("/api/storage/files/{storageFileId:guid}/claim", Tags = ["Storage"],
        ActorRequirement = ActorRequirement.Required,
        TenantAccessMode = TenantAccessMode.ActorTenant,
        RequiredServiceScopes = [],
        AllowedServiceCallers = [],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.Manage],
        Capability = StorageAuthorizationCapabilities.ManageKey,
        Summary = "Claim storage file",
        Description = "Idempotently claims a completed file so unclaimed-file maintenance will not delete it.")]
    public static Task<Result<StorageFileResponse>> HandleHttp(
        ClaimStorageFileRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.ClaimFileAsync(request, ct);
}

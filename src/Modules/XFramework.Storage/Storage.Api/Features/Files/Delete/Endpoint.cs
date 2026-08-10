using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Storage.Api.Features.Files.Delete;

public static class DeleteStorageFileEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.IdentityServer, XFrameworkServiceNames.Portal])]
    [MapDelete("/api/storage/files/{storageFileId:guid}", Tags = ["Storage"],
        ActorRequirement = ActorRequirement.Required,
        TenantAccessMode = TenantAccessMode.ActorTenant,
        RequiredServiceScopes = [],
        AllowedServiceCallers = [],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.Manage],
        Capability = StorageAuthorizationCapabilities.ManageKey,
        Summary = "Delete storage file",
        Description = "Soft-deletes storage metadata and schedules physical deletion by retention cleanup.")]
    public static Task<Result> Handle(
        DeleteStorageFileRequest request,
        StorageService storageService,
        CancellationToken ct) =>
        storageService.DeleteFileAsync(request, ct);
}

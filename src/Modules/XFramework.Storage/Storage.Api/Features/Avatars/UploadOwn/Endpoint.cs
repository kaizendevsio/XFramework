using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.Avatars.UploadOwn;

public static class UploadOwnAvatarFileEndpoint
{
    // Identity has already authorized the profile operation. Ordinary users do not
    // need Storage management permission; this route only creates their own avatar.
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite],
        RequiredActorCapabilities = [],
        AllowedServiceCallers = [XFrameworkServiceNames.IdentityServer])]
    public static Task<Result<StorageFileResponse>> Handle(
        UploadOwnAvatarFileRequest request, StorageService storage, CancellationToken ct) =>
        storage.UploadOwnAvatarFileAsync(request, ct);
}

using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Credentials.OwnAvatar;

public static class UploadOwnAvatarEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityProfile])]
    public static Task<Result<CredentialAvatarResponse>> Handle(
        UploadOwnAvatarRequest request, IAuthService auth, CancellationToken ct) =>
        auth.UploadOwnAvatarAsync(request, ct);
}

using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Encryption.ResetEncryptionIdentity;

public static class ResetEncryptionIdentityEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityProfile])]
    public static Task<Result<EncryptionDirectoryResponse>> Handle(
        ResetEncryptionIdentityRequest request, EncryptionDirectoryService service, IAuthService auth, CancellationToken ct) =>
        service.ResetEncryptionIdentityAsync(request, auth, ct);
}

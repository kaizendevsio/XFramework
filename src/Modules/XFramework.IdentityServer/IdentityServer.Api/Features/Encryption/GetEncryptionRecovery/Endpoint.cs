using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Encryption.GetEncryptionRecovery;

public static class GetEncryptionRecoveryEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityProfile])]
    public static Task<Result<EncryptionRecoveryResponse>> Handle(
        GetEncryptionRecoveryRequest request, EncryptionDirectoryService service, CancellationToken ct) =>
        service.GetEncryptionRecoveryAsync(request, ct);
}

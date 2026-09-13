using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Encryption.PutEncryptionRecovery;

public static class PutEncryptionRecoveryEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityProfile])]
    public static Task<Result<EncryptionRecoveryResponse>> Handle(
        PutEncryptionRecoveryRequest request, EncryptionDirectoryService service, CancellationToken ct) =>
        service.PutEncryptionRecoveryAsync(request, ct);
}

using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Encryption.PutEncryptionDirectory;

public static class PutEncryptionDirectoryEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityProfile])]
    public static Task<Result<EncryptionDirectoryResponse>> Handle(
        PutEncryptionDirectoryRequest request, EncryptionDirectoryService service, CancellationToken ct) =>
        service.PutEncryptionDirectoryAsync(request, ct);
}

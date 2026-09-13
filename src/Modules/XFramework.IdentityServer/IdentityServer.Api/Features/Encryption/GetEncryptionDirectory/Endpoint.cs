using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Encryption.GetEncryptionDirectory;

public static class GetEncryptionDirectoryEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityProfile])]
    public static Task<Result<EncryptionDirectoryResponse>> Handle(
        GetEncryptionDirectoryRequest request, EncryptionDirectoryService service, CancellationToken ct) =>
        service.GetEncryptionDirectoryAsync(request, ct);
}

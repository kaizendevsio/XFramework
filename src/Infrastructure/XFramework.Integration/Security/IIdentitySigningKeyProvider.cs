using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public interface IIdentitySigningKeyProvider
{
    Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
        string? keyId = null,
        CancellationToken ct = default);
}

using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public interface IIdentitySigningKeyProvider
{
    Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
        string? keyId = null,
        CancellationToken ct = default);
}

public interface IServiceCredentialGenerationProvider
{
    Task<bool> IsAcceptedAsync(
        string clientId,
        string generationId,
        CancellationToken ct = default);
}

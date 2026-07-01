using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Services;

public sealed class IdentityServerLocalSigningKeyProvider(IServiceScopeFactory scopeFactory)
    : IIdentitySigningKeyProvider
{
    public async Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
        string? keyId = null,
        CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IServiceIdentityService>();
        var result = await service.GetSigningKeysAsync(new GetServiceSigningKeysRequest { KeyId = keyId }, ct);
        if (!result.IsSuccess || result.Data is null)
            throw new InvalidOperationException(result.Message ?? "IdentityServer signing keys are unavailable.");

        return result.Data.Keys;
    }
}

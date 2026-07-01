using System.Collections.Concurrent;
using Bolt.Client;
using MemoryPack;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public sealed class IdentityServerSigningKeyProvider(
    BoltClient boltClient,
    IOptions<ServiceIdentityOptions> options)
    : IIdentitySigningKeyProvider
{
    private readonly ConcurrentDictionary<string, CachedKeys> _cache = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
        string? keyId = null,
        CancellationToken ct = default)
    {
        var cacheKey = string.IsNullOrWhiteSpace(keyId) ? "*" : keyId.Trim();
        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > now)
            return cached.Keys;

        var request = new GetServiceSigningKeysRequest
        {
            KeyId = keyId,
            Metadata = new RequestMetadata
            {
                Name = options.Value.ClientId,
                RequestId = Guid.NewGuid()
            }
        };

        var (status, data) = await boltClient.InvokeAsync(
            XFrameworkServiceNames.IdentityServer.ToSha256(),
            nameof(GetServiceSigningKeysRequest),
            MemoryPackSerializer.Serialize(request),
            ct);

        if ((int)status < 200 || (int)status >= 300)
            throw new InvalidOperationException($"Signing-key request failed with status {(int)status} ({status}).");

        var response = MemoryPackSerializer.Deserialize<QueryResponse<ServiceSigningKeysResponse>>(data.Span)
            ?? throw new InvalidOperationException("Signing-key response could not be deserialized.");

        if (!response.IsSuccess || response.Response is null)
            throw new InvalidOperationException(response.Message ?? "IdentityServer signing keys were unavailable.");

        var keys = response.Response.Keys;
        var cacheMinutes = Math.Clamp(options.Value.SigningKeyCacheMinutes, 1, 60);
        _cache[cacheKey] = new CachedKeys(keys, now.AddMinutes(cacheMinutes));
        return keys;
    }

    private sealed record CachedKeys(IReadOnlyList<ServiceSigningKeyResponse> Keys, DateTime ExpiresAtUtc);
}

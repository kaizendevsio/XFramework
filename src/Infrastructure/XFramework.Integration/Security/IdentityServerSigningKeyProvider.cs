using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public sealed class IdentityServerSigningKeyProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceIdentityOptions> options)
    : IIdentitySigningKeyProvider
{
    private static readonly TimeSpan UnknownKeyRefreshInterval = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CachedKeys? _cache;
    private DateTime _lastUnknownKeyRefreshAttemptUtc = DateTime.MinValue;
    private Exception? _lastUnknownKeyRefreshError;

    public async Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
        string? keyId = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (_cache is { } cached && cached.ExpiresAtUtc > now)
        {
            var selected = SelectKeys(cached.Keys, keyId);
            if (string.IsNullOrWhiteSpace(keyId) || selected.Count > 0)
                return selected;
        }

        await _refreshGate.WaitAsync(ct);
        try
        {
            now = DateTime.UtcNow;
            var requestedSpecificKey = !string.IsNullOrWhiteSpace(keyId);
            var refreshingUnknownKey = false;
            if (_cache is { } refreshed && refreshed.ExpiresAtUtc > now)
            {
                var selected = SelectKeys(refreshed.Keys, keyId);
                if (string.IsNullOrWhiteSpace(keyId) || selected.Count > 0)
                    return selected;

                if (now - _lastUnknownKeyRefreshAttemptUtc < UnknownKeyRefreshInterval)
                {
                    if (_lastUnknownKeyRefreshError is { } refreshError)
                    {
                        throw new InvalidOperationException(
                            "IdentityServer signing-key refresh is temporarily unavailable.",
                            refreshError);
                    }

                    return selected;
                }

                refreshingUnknownKey = true;
                _lastUnknownKeyRefreshAttemptUtc = now;
                _lastUnknownKeyRefreshError = null;
            }
            else if (requestedSpecificKey &&
                     _lastUnknownKeyRefreshError is { } previousError &&
                     now - _lastUnknownKeyRefreshAttemptUtc < UnknownKeyRefreshInterval)
            {
                throw new InvalidOperationException(
                    "IdentityServer signing-key refresh is temporarily unavailable.",
                    previousError);
            }

            // Always fetch the bounded active set. The JWT kid is untrusted and must not
            // become an HTTP/cache partition key.
            var request = new GetServiceSigningKeysRequest
            {
                Metadata = new RequestMetadata
                {
                    Name = options.Value.ClientId,
                    RequestId = Guid.NewGuid()
                }
            };

            ServiceSigningKeysResponse response;
            try
            {
                response = await ServiceIdentityHttpClient.PostForResponseAsync<
                    GetServiceSigningKeysRequest,
                    ServiceSigningKeysResponse>(
                    httpClientFactory,
                    options.Value,
                    ServiceIdentityHttpClient.SigningKeysPath,
                    request,
                    ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                if (requestedSpecificKey)
                {
                    _lastUnknownKeyRefreshAttemptUtc = DateTime.UtcNow;
                    _lastUnknownKeyRefreshError = ex;
                }

                throw;
            }

            now = DateTime.UtcNow;
            var keys = response.Keys;
            var cacheMinutes = Math.Clamp(options.Value.SigningKeyCacheMinutes, 1, 60);
            _cache = new CachedKeys(keys, now.AddMinutes(cacheMinutes));
            var result = SelectKeys(keys, keyId);
            if (refreshingUnknownKey || (requestedSpecificKey && result.Count == 0))
                _lastUnknownKeyRefreshAttemptUtc = now;
            if (requestedSpecificKey)
                _lastUnknownKeyRefreshError = null;
            return result;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static IReadOnlyList<ServiceSigningKeyResponse> SelectKeys(
        IReadOnlyList<ServiceSigningKeyResponse> keys,
        string? keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            return keys;

        var expectedKeyId = keyId.Trim();
        return keys
            .Where(key => string.Equals(key.KeyId, expectedKeyId, StringComparison.Ordinal))
            .ToList();
    }

    private sealed record CachedKeys(IReadOnlyList<ServiceSigningKeyResponse> Keys, DateTime ExpiresAtUtc);
}

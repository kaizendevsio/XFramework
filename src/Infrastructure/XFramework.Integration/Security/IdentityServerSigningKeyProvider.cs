using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public sealed class IdentityServerSigningKeyProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceIdentityOptions> options)
    : IIdentitySigningKeyProvider, IServiceCredentialGenerationProvider
{
    private static readonly TimeSpan UnknownKeyRefreshInterval = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CachedKeys? _cache;
    private DateTime _lastUnknownKeyRefreshAttemptUtc = DateTime.MinValue;
    private DateTime _lastGenerationRefreshAttemptUtc = DateTime.MinValue;
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
                    OperationName = "Get service signing keys",
                    RequestId = Guid.NewGuid()
                }
            };

            ServiceSigningKeysResponse response;
            try
            {
                response = await FetchAsync(request, ct);
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
            _cache = new CachedKeys(
                keys,
                response.CredentialGenerationsByClient,
                now.AddMinutes(cacheMinutes),
                GetGenerationPolicyExpiry(now));
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

    public async Task<bool> IsAcceptedAsync(
        string clientId,
        string generationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(generationId))
            return false;

        var now = DateTime.UtcNow;
        if (_cache is { } cached && cached.GenerationPolicyExpiresAtUtc > now &&
            IsAccepted(cached, clientId, generationId))
        {
            return true;
        }

        await _refreshGate.WaitAsync(ct);
        try
        {
            now = DateTime.UtcNow;
            if (_cache is { } refreshed && refreshed.GenerationPolicyExpiresAtUtc > now &&
                IsAccepted(refreshed, clientId, generationId))
            {
                return true;
            }

            if (now - _lastGenerationRefreshAttemptUtc < GetGenerationPolicyRefreshInterval())
                return false;

            _lastGenerationRefreshAttemptUtc = now;
            var response = await FetchAsync(
                new GetServiceSigningKeysRequest
                {
                    Metadata = new RequestMetadata
                    {
                        OperationName = "Refresh service credential generations",
                        RequestId = Guid.NewGuid()
                    }
                },
                ct);
            var cacheMinutes = Math.Clamp(options.Value.SigningKeyCacheMinutes, 1, 60);
            _cache = new CachedKeys(
                response.Keys,
                response.CredentialGenerationsByClient,
                DateTime.UtcNow.AddMinutes(cacheMinutes),
                GetGenerationPolicyExpiry(DateTime.UtcNow));
            return IsAccepted(_cache, clientId, generationId);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<ServiceSigningKeysResponse> FetchAsync(
        GetServiceSigningKeysRequest request,
        CancellationToken ct) =>
        await ServiceIdentityHttpClient.PostForResponseAsync<
            GetServiceSigningKeysRequest,
            ServiceSigningKeysResponse>(
            httpClientFactory,
            options.Value,
            ServiceIdentityHttpClient.SigningKeysPath,
            request,
            ct);

    private static bool IsAccepted(
        CachedKeys cached,
        string clientId,
        string generationId) =>
        cached.CredentialGenerationsByClient.TryGetValue(clientId, out var generations) &&
        generations.Contains(generationId, StringComparer.Ordinal);

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

    private DateTime GetGenerationPolicyExpiry(DateTime now) =>
        now.Add(GetGenerationPolicyRefreshInterval());

    private TimeSpan GetGenerationPolicyRefreshInterval() =>
        TimeSpan.FromSeconds(Math.Clamp(options.Value.CredentialGenerationCacheSeconds, 0, 60));

    private sealed record CachedKeys(
        IReadOnlyList<ServiceSigningKeyResponse> Keys,
        IReadOnlyDictionary<string, List<string>> CredentialGenerationsByClient,
        DateTime ExpiresAtUtc,
        DateTime GenerationPolicyExpiresAtUtc);
}

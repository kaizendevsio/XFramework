using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public static class ServiceIdentityHttpClient
{
    public const string Name = "XFramework.ServiceIdentity";

    internal const string BoltTransportTokenPath = "/api/service-identity/bolt-transport-token";
    internal const string ServiceTokenPath = "/api/service-identity/token";
    internal const string SigningKeysPath = "/api/service-identity/signing-keys/query";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal static async Task<ServiceTokenResponse> PostForTokenAsync<TRequest>(
        IHttpClientFactory httpClientFactory,
        ServiceIdentityOptions options,
        string path,
        TRequest request,
        CancellationToken ct)
    {
        var authority = options.ResolveAuthority();
        var endpoint = new Uri(authority, path);
        var client = httpClientFactory.CreateClient(Name);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        using var response = await client.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"IdentityServer token request failed with HTTP status {(int)response.StatusCode} ({response.StatusCode}).");
        }

        var token = await response.Content.ReadFromJsonAsync<ServiceTokenResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("IdentityServer token response was empty.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("IdentityServer did not issue a token.");

        return token;
    }

    internal static async Task<TResponse> PostForResponseAsync<TRequest, TResponse>(
        IHttpClientFactory httpClientFactory,
        ServiceIdentityOptions options,
        string path,
        TRequest request,
        CancellationToken ct)
    {
        var authority = options.ResolveAuthority();
        var endpoint = new Uri(authority, path);
        var client = httpClientFactory.CreateClient(Name);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        using var response = await client.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"IdentityServer request failed with HTTP status {(int)response.StatusCode} ({response.StatusCode}).");
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("IdentityServer response was empty.");
    }
}

internal sealed class IdentityServerTokenCache(
    IOptions<ServiceIdentityOptions> serviceIdentityOptions,
    TimeProvider timeProvider,
    ILogger logger)
{
    private static readonly TimeSpan FailureRetryBackoff = TimeSpan.FromMilliseconds(500);
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _retryNotBefore = new(StringComparer.Ordinal);

    public async ValueTask<string> GetTokenAsync(
        string cacheKey,
        string tokenKind,
        Func<CancellationToken, Task<ServiceTokenResponse>> acquireToken,
        CancellationToken ct)
    {
        if (TryGetCachedToken(cacheKey, out var accessToken))
            return accessToken;

        ct.ThrowIfCancellationRequested();
        var acquisition = _inflight.GetOrAdd(
            cacheKey,
            _ => CreateAcquisition(cacheKey, tokenKind, acquireToken));

        return await acquisition.Value.WaitAsync(ct);
    }

    private Lazy<Task<string>> CreateAcquisition(
        string cacheKey,
        string tokenKind,
        Func<CancellationToken, Task<ServiceTokenResponse>> acquireToken)
    {
        Lazy<Task<string>>? acquisition = null;
        acquisition = new Lazy<Task<string>>(
            async () =>
            {
                try
                {
                    var timeout = ResolveTokenAcquisitionTimeout();
                    using var timeoutCts = new CancellationTokenSource(timeout);
                    try
                    {
                        await WaitForFailureBackoffAsync(cacheKey, timeoutCts.Token);
                        var token = await AcquireAndCacheTokenAsync(cacheKey, acquireToken, timeoutCts.Token);
                        _retryNotBefore.TryRemove(cacheKey, out _);
                        return token;
                    }
                    catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
                    {
                        throw new TimeoutException(
                            $"IdentityServer {tokenKind} token acquisition timed out after {timeout.TotalSeconds:0} seconds.",
                            ex);
                    }
                }
                catch (Exception exception)
                {
                    _retryNotBefore[cacheKey] = timeProvider.GetUtcNow().Add(FailureRetryBackoff);
                    logger.LogWarning(
                        exception,
                        "IdentityServer token acquisition failed. TokenKind={TokenKind}",
                        tokenKind);
                    throw;
                }
                finally
                {
                    if (_inflight.TryGetValue(cacheKey, out var current) && ReferenceEquals(current, acquisition))
                        _inflight.TryRemove(cacheKey, out _);
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication);

        return acquisition;
    }

    private async Task WaitForFailureBackoffAsync(string cacheKey, CancellationToken ct)
    {
        if (!_retryNotBefore.TryGetValue(cacheKey, out var retryNotBefore))
            return;

        var delay = retryNotBefore - timeProvider.GetUtcNow();
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, timeProvider, ct);
    }

    private async Task<string> AcquireAndCacheTokenAsync(
        string cacheKey,
        Func<CancellationToken, Task<ServiceTokenResponse>> acquireToken,
        CancellationToken ct)
    {
        if (TryGetCachedToken(cacheKey, out var cachedAccessToken))
            return cachedAccessToken;

        var response = await acquireToken(ct);
        var expiresAtUtc = ToUtc(response.ExpiresAtUtc);
        if (expiresAtUtc <= timeProvider.GetUtcNow())
            throw new InvalidOperationException("IdentityServer issued a token without a future expiry.");

        _cache[cacheKey] = new CachedToken(response.AccessToken, expiresAtUtc);
        return response.AccessToken;
    }

    private bool TryGetCachedToken(string cacheKey, out string accessToken)
    {
        var refreshSkew = TimeSpan.FromSeconds(
            Math.Clamp(serviceIdentityOptions.Value.TokenRefreshSkewSeconds, 0, 600));
        var validAfterUtc = timeProvider.GetUtcNow().Add(refreshSkew);
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > validAfterUtc)
        {
            accessToken = cached.AccessToken;
            return true;
        }

        accessToken = string.Empty;
        return false;
    }

    private TimeSpan ResolveTokenAcquisitionTimeout()
    {
        var configuredSeconds = serviceIdentityOptions.Value.TokenAcquisitionTimeoutSeconds;
        var timeoutSeconds = Math.Clamp(configuredSeconds > 0 ? configuredSeconds : 30, 1, 300);
        return TimeSpan.FromSeconds(timeoutSeconds);
    }

    private static DateTimeOffset ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(value),
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
    };

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
}

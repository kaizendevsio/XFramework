using System.Collections.Concurrent;
using System.Threading;
using Bolt.Client;
using MemoryPack;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public sealed class IdentityServerServiceTokenProvider(
    BoltClient boltClient,
    IOptions<ServiceIdentityOptions> serviceIdentityOptions,
    IOptions<BoltConfiguration> boltConfigurationOptions)
    : IServiceTokenProvider
{
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new(StringComparer.Ordinal);

    public async ValueTask<string> GetTokenAsync(
        string audience,
        IReadOnlyCollection<string>? scopes = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Service token audience is required.");

        var requestedScopes = NormalizeScopes(scopes);
        var cacheKey = $"{audience}|{string.Join(' ', requestedScopes)}";
        var now = DateTime.UtcNow;
        var options = serviceIdentityOptions.Value;
        var refreshSkew = TimeSpan.FromSeconds(Math.Clamp(options.TokenRefreshSkewSeconds, 10, 600));

        if (_cache.TryGetValue(cacheKey, out var cached) &&
            cached.ExpiresAtUtc > now.Add(refreshSkew))
        {
            return cached.AccessToken;
        }

        ct.ThrowIfCancellationRequested();

        var acquisition = _inflight.GetOrAdd(
            cacheKey,
            _ => CreateAcquisition(audience, requestedScopes, cacheKey));

        return await acquisition.Value.WaitAsync(ct);
    }

    private Lazy<Task<string>> CreateAcquisition(
        string audience,
        IReadOnlyCollection<string> requestedScopes,
        string cacheKey)
    {
        Lazy<Task<string>>? acquisition = null;
        acquisition = new Lazy<Task<string>>(
            async () =>
            {
                try
                {
                    return await AcquireTokenAsync(audience, requestedScopes, cacheKey, CancellationToken.None);
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

    private async Task<string> AcquireTokenAsync(
        string audience,
        IReadOnlyCollection<string> requestedScopes,
        string cacheKey,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var options = serviceIdentityOptions.Value;
        var refreshSkew = TimeSpan.FromSeconds(Math.Clamp(options.TokenRefreshSkewSeconds, 10, 600));

        if (_cache.TryGetValue(cacheKey, out var cached) &&
            cached.ExpiresAtUtc > now.Add(refreshSkew))
        {
            return cached.AccessToken;
        }

        var clientId = ResolveClientId();
        var clientSecret = options.ClientSecret;
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                $"ServiceIdentity:ClientSecret is required for service client '{clientId}'.");
        }

        var request = new IssueServiceTokenRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Audience = audience,
            Scopes = requestedScopes.ToList(),
            Metadata = new RequestMetadata
            {
                Name = clientId,
                RequestId = Guid.NewGuid()
            }
        };

        var targetClient = XFrameworkServiceNames.IdentityServer.ToSha256();
        var (status, data) = await boltClient.InvokeAsync(
            targetClient,
            nameof(IssueServiceTokenRequest),
            MemoryPackSerializer.Serialize(request),
            ct);

        if ((int)status < 200 || (int)status >= 300)
            throw new InvalidOperationException($"Service token request failed with status {(int)status} ({status}).");

        var response = MemoryPackSerializer.Deserialize<QueryResponse<ServiceTokenResponse>>(data.Span)
            ?? throw new InvalidOperationException("Service token response could not be deserialized.");

        if (!response.IsSuccess || response.Response is null || string.IsNullOrWhiteSpace(response.Response.AccessToken))
        {
            throw new InvalidOperationException(
                response.Message ?? "IdentityServer did not issue a service token.");
        }

        _cache[cacheKey] = new CachedToken(response.Response.AccessToken, response.Response.ExpiresAtUtc);
        return response.Response.AccessToken;
    }

    private string ResolveClientId()
    {
        var configured = serviceIdentityOptions.Value.ClientId;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var boltName = boltConfigurationOptions.Value.ClientName;
        if (!string.IsNullOrWhiteSpace(boltName))
            return boltName.Trim();

        throw new InvalidOperationException("ServiceIdentity:ClientId or BoltConfiguration:ClientName is required.");
    }

    private List<string> NormalizeScopes(IReadOnlyCollection<string>? scopes)
    {
        var defaults = serviceIdentityOptions.Value.DefaultScopes;
        var source = scopes is { Count: > 0 } ? scopes : defaults;

        return source
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record CachedToken(string AccessToken, DateTime ExpiresAtUtc);
}

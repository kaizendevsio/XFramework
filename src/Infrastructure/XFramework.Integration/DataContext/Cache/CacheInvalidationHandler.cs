using Microsoft.Extensions.Logging;
using XFramework.Integration.Abstractions;

namespace XFramework.Integration.DataContext.Cache;

/// <summary>
/// Handles server-push cache invalidation notifications from the Bolt hub.
/// Bolt push invalidation is parked until a hub notification contract exists.
/// </summary>
public sealed class CacheInvalidationHandler : IDisposable
{
    private readonly IClientCacheService _cache;
    private readonly ILogger<CacheInvalidationHandler> _logger;

    public CacheInvalidationHandler(
        IClientCacheService cache,
        ILogger<CacheInvalidationHandler> logger)
    {
        _cache = cache;
        _logger = logger;

        _logger.LogDebug("Remote data-context server-push cache invalidation is disabled; use manual invalidation APIs.");
    }

    public bool ServerPushInvalidationEnabled => false;

    public Task InvalidatePrefixAsync(string prefix, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Cache invalidation prefix is required.", nameof(prefix));

        _logger.LogDebug("Invalidating remote data-context client cache entries with prefix {Prefix}", prefix);
        return _cache.RemoveByPrefixAsync(prefix, ct);
    }

    public Task ClearAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Clearing all remote data-context client cache entries");
        return _cache.ClearAllAsync(ct);
    }

    public void Dispose()
    {
    }
}

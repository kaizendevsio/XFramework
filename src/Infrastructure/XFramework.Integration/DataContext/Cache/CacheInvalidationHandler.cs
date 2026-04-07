using Microsoft.Extensions.Logging;
using XFramework.Integration.Abstractions;

namespace XFramework.Integration.DataContext.Cache;

/// <summary>
/// Handles server-push cache invalidation notifications from the Bolt hub.
/// Migration to Bolt thin protocol push notifications is parked work (see Task 14).
/// </summary>
public class CacheInvalidationHandler
{
    private readonly IClientCacheService _cache;
    private readonly ILogger<CacheInvalidationHandler> _logger;

    public CacheInvalidationHandler(
        IClientCacheService cache,
        ILogger<CacheInvalidationHandler> logger)
    {
        _cache = cache;
        _logger = logger;

        // TODO (Task 14): Subscribe to Bolt thin-protocol push notifications for cache invalidation.
        // The previous SignalR-based subscription has been removed.
    }

    public void Dispose()
    {
    }
}

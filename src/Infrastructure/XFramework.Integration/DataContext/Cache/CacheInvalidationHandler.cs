using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Abstractions;

namespace XFramework.Integration.DataContext.Cache;

public class CacheInvalidationHandler
{
    private readonly IClientCacheService _cache;
    private readonly ILogger<CacheInvalidationHandler> _logger;
    private IDisposable? _subscription;

    public CacheInvalidationHandler(
        IClientCacheService cache,
        ISignalRService signalRService,
        ILogger<CacheInvalidationHandler> logger)
    {
        _cache = cache;
        _logger = logger;

        if (signalRService.Connection is not null)
        {
            Subscribe(signalRService.Connection);
        }
    }

    private void Subscribe(HubConnection connection)
    {
        _subscription = connection.On<string[]>("InvalidateCache", async entityTypeNames =>
        {
            foreach (var entityType in entityTypeNames)
            {
                _logger.LogDebug("Server-push cache invalidation for entity type '{EntityType}'", entityType);
                await _cache.RemoveByPrefixAsync(CacheKeyBuilder.PrefixForEntity(entityType));
            }
        });
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}

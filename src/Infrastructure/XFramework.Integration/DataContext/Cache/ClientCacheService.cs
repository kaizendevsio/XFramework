using System.Collections.Concurrent;

namespace XFramework.Integration.DataContext.Cache;

public class ClientCacheService : IClientCacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _l1Cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (_l1Cache.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired)
            {
                _l1Cache.TryRemove(key, out _);
                return Task.FromResult<T?>(default);
            }

            var value = MemoryPack.MemoryPackSerializer.Deserialize<T>(entry.Data);
            return Task.FromResult(value);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default)
    {
        var bytes = MemoryPack.MemoryPackSerializer.Serialize(value);
        var entry = new CacheEntry
        {
            Data = bytes,
            ExpiresAtUtc = DateTime.UtcNow.Add(expiration),
            EntityTypeName = typeof(T).Name
        };

        _l1Cache[key] = entry;
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var keysToRemove = _l1Cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var key in keysToRemove)
        {
            _l1Cache.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task ClearAllAsync(CancellationToken ct = default)
    {
        _l1Cache.Clear();
        return Task.CompletedTask;
    }
}

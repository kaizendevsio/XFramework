using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Extensions;

namespace XFramework.Core.Services;

public class CacheManager(IMemoryCache memoryCache, ILogger<CacheManager> logger)
{
    private readonly HashSet<string> _cacheKeys = new();
    private readonly object _lock = new object();
    private readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        // Set the cache expiration as needed
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    /// <summary>
    ///  This method can be used to cache an item
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public async Task Set(string key, object value)
    {
        memoryCache.Set(key, value, _cacheOptions);
        lock (_lock)
        {
            _cacheKeys.Add(key);
        }
        logger.LogInformation("Cached item with key {CacheKey}", key);
    }

    /// <summary>
    ///  This method can be used to retrieve a cached item
    /// </summary>
    /// <param name="key"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? Get<T>(string key)
    {
        memoryCache.TryGetValue(key, out T? value);
        if (value is not null)
        {
            logger.LogInformation("Retrieved cached item with key {CacheKey}", key);
        }
        return value;
    }

    /// <summary>
    ///  This method can be used to remove a specific cached item
    /// </summary>
    /// <param name="key"></param>
    public void Remove(string key)
    {
        memoryCache.Remove(key);
        lock (_lock)
        {
            _cacheKeys.Remove(key);
        }
        logger.LogInformation("Removed cached item with key {CacheKey}", key);
    }
    
    /// <summary>
    ///   This method can be used to invalidate all cached items that contain a specific property name
    /// </summary>
    /// <param name="propName"></param>
    public void InvalidateCacheContaining(string propName)
    {
        var keysToRemove = _cacheKeys.Where(key => key.Contains(propName)).ToList();
        foreach (var key in keysToRemove)
        {
           Remove(key);
        }
    }

    /// <summary>
    ///  This method can be used to invalidate all cached items
    /// </summary>
    public void ClearAll()
    {
        logger.LogInformation("Clearing all cached items");
        lock (_lock)
        {
            foreach (var key in _cacheKeys)
            {
                memoryCache.Remove(key);
            }
            _cacheKeys.Clear();
        }
        logger.LogInformation("Cleared all cached items");
    }
    
    public async Task InvalidateCacheForModel<T>(T model) 
        where T : class
    {
        var idPropertyNames = model.GetIdPropertyNames();

        foreach (var propName in idPropertyNames)
        {
            InvalidateCacheContaining(propName.Value.ToString());
        }
    }

}

using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using XFramework.Core.Patterns;

namespace XFramework.Core.Services.Caching;

/// <summary>
/// Hybrid caching service that combines in-memory (L1) and distributed Redis (L2) caching.
/// Provides graceful fallback to L1 only if Redis is unavailable.
/// </summary>
public class HybridCacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly IConnectionMultiplexer? _redisConnection;
    private readonly CacheOptions _options;
    private readonly ILogger<HybridCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // Statistics tracking
    private long _totalGets;
    private long _hits;
    private long _misses;
    private bool _redisAvailable;

    public HybridCacheService(
        IMemoryCache memoryCache,
        IDistributedCache? distributedCache,
        IConnectionMultiplexer? redisConnection,
        IOptions<CacheOptions> options,
        ILogger<HybridCacheService> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _distributedCache = distributedCache;
        _redisConnection = redisConnection;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        // Check Redis availability
        _redisAvailable = CheckRedisAvailability();
        
        if (!_redisAvailable && _options.EnableL2Cache)
        {
            _logger.LogWarning("Redis is not available. Cache will operate in L1 (memory) only mode");
        }
    }

    /// <inheritdoc />
    public async Task<Result<T?>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Result<T?>.Success(default, "Caching is disabled");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<T?>.Failure("Cache key cannot be null or empty", 400);
        }

        try
        {
            if (_options.EnableStatistics)
            {
                Interlocked.Increment(ref _totalGets);
            }

            // Try L1 (Memory) first
            if (_options.EnableL1Cache && _memoryCache.TryGetValue(key, out T? cachedValue))
            {
                if (_options.EnableStatistics)
                {
                    Interlocked.Increment(ref _hits);
                }
                
                _logger.LogTrace("Cache hit (L1) for key: {Key}", key);
                return Result<T?>.Success(cachedValue);
            }

            // Try L2 (Redis) if available
            if (_options.EnableL2Cache && _redisAvailable && _distributedCache != null)
            {
                var redisValue = await _distributedCache.GetStringAsync(key, cancellationToken);
                
                if (!string.IsNullOrEmpty(redisValue))
                {
                    var deserializedValue = JsonSerializer.Deserialize<T>(redisValue, _jsonOptions);
                    
                    // Populate L1 cache
                    if (_options.EnableL1Cache)
                    {
                        SetInMemoryCache(key, deserializedValue);
                    }

                    if (_options.EnableStatistics)
                    {
                        Interlocked.Increment(ref _hits);
                    }
                    
                    _logger.LogTrace("Cache hit (L2) for key: {Key}", key);
                    return Result<T?>.Success(deserializedValue);
                }
            }

            if (_options.EnableStatistics)
            {
                Interlocked.Increment(ref _misses);
            }
            
            _logger.LogTrace("Cache miss for key: {Key}", key);
            return Result<T?>.Success(default, "Cache miss");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache key: {Key}", key);
            return Result<T?>.Failure($"Cache retrieval failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Result.Success("Caching is disabled");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure("Cache key cannot be null or empty", 400);
        }

        try
        {
            // Set in L1 (Memory)
            if (_options.EnableL1Cache)
            {
                SetInMemoryCache(key, value, absoluteExpiration, slidingExpiration);
            }

            // Set in L2 (Redis)
            if (_options.EnableL2Cache && _redisAvailable && _distributedCache != null)
            {
                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = CreateDistributedCacheOptions(absoluteExpiration, slidingExpiration);
                
                await _distributedCache.SetStringAsync(key, jsonValue, options, cancellationToken);
            }

            _logger.LogTrace("Cache set for key: {Key}", key);
            return Result.Success("Cache entry set successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key: {Key}", key);
            
            // If Redis failed but memory cache succeeded, consider it partial success
            if (_options.EnableGracefulDegradation && _options.EnableL1Cache)
            {
                return Result.Success("Cache entry set in L1 only (L2 unavailable)");
            }
            
            return Result.Failure($"Cache set failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure("Cache key cannot be null or empty", 400);
        }

        try
        {
            // Remove from L1 (Memory)
            if (_options.EnableL1Cache)
            {
                _memoryCache.Remove(key);
            }

            // Remove from L2 (Redis)
            if (_options.EnableL2Cache && _redisAvailable && _distributedCache != null)
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
            }

            _logger.LogTrace("Cache removed for key: {Key}", key);
            return Result.Success("Cache entry removed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key: {Key}", key);
            return Result.Failure($"Cache removal failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<bool>.Failure("Cache key cannot be null or empty", 400);
        }

        try
        {
            // Check L1 (Memory) first
            if (_options.EnableL1Cache && _memoryCache.TryGetValue(key, out _))
            {
                return Result<bool>.Success(true);
            }

            // Check L2 (Redis)
            if (_options.EnableL2Cache && _redisAvailable && _redisConnection != null)
            {
                var db = _redisConnection.GetDatabase();
                var exists = await db.KeyExistsAsync(key);
                return Result<bool>.Success(exists);
            }

            return Result<bool>.Success(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache key existence: {Key}", key);
            return Result<bool>.Failure($"Cache existence check failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<T>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<T>.Failure("Cache key cannot be null or empty", 400);
        }

        if (factory == null)
        {
            return Result<T>.Failure("Factory function cannot be null", 400);
        }

        try
        {
            // Try to get from cache
            var getResult = await GetAsync<T>(key, cancellationToken);
            
            if (getResult.IsSuccess && getResult.Data != null)
            {
                return Result<T>.Success(getResult.Data);
            }

            // Cache miss - execute factory
            _logger.LogTrace("Cache miss, executing factory for key: {Key}", key);
            var value = await factory(cancellationToken);

            // Set in cache
            var setResult = await SetAsync(key, value, absoluteExpiration, slidingExpiration, cancellationToken);
            
            if (!setResult.IsSuccess)
            {
                _logger.LogWarning("Failed to cache value for key {Key}: {Message}", key, setResult.Message);
            }

            return Result<T>.Success(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrSetAsync for key: {Key}", key);
            return Result<T>.Failure($"GetOrSet operation failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Result<int>.Failure("Prefix cannot be null or empty", 400);
        }

        try
        {
            int removedCount = 0;

            // Note: L1 (MemoryCache) doesn't support pattern-based removal efficiently
            // This is a limitation of IMemoryCache. We can only remove from L2 (Redis).
            _logger.LogWarning("L1 cache does not support prefix-based removal. Only L2 (Redis) entries will be removed");

            // Remove from L2 (Redis) using SCAN
            if (_options.EnableL2Cache && _redisAvailable && _redisConnection != null)
            {
                var db = _redisConnection.GetDatabase();
                var server = _redisConnection.GetServer(_redisConnection.GetEndPoints().First());
                
                var pattern = $"{prefix}*";
                var keys = server.Keys(pattern: pattern, pageSize: 1000);

                foreach (var key in keys)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await db.KeyDeleteAsync(key);
                    removedCount++;
                }

                _logger.LogInformation("Removed {Count} cache entries with prefix: {Prefix}", removedCount, prefix);
            }

            return Result<int>.Success(removedCount, $"Removed {removedCount} entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache entries by prefix: {Prefix}", prefix);
            return Result<int>.Failure($"Prefix removal failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public Result<CacheStatistics> GetStatistics()
    {
        try
        {
            var stats = new CacheStatistics
            {
                TotalGets = _totalGets,
                Hits = _hits,
                Misses = _misses,
                L1EntryCount = GetMemoryCacheCount(),
                L2Available = _redisAvailable
            };

            return Result<CacheStatistics>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache statistics");
            return Result<CacheStatistics>.Failure($"Statistics retrieval failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear L1 (Memory) - note: IMemoryCache doesn't have a built-in Clear method
            // We would need to implement a wrapper or use reflection, but for safety we'll skip this
            _logger.LogWarning("L1 cache clearing is not fully supported by IMemoryCache. Only L2 will be cleared");

            // Clear L2 (Redis) by removing all keys
            if (_options.EnableL2Cache && _redisAvailable && _redisConnection != null)
            {
                var server = _redisConnection.GetServer(_redisConnection.GetEndPoints().First());
                await server.FlushDatabaseAsync();
                _logger.LogWarning("L2 (Redis) cache cleared");
            }

            // Reset statistics
            _totalGets = 0;
            _hits = 0;
            _misses = 0;

            return Result.Success("Cache cleared successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return Result.Failure($"Cache clear failed: {ex.Message}", 500);
        }
    }

    // Private helper methods

    private void SetInMemoryCache<T>(
        string key,
        T? value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null)
    {
        var cacheOptions = new MemoryCacheEntryOptions();

        // Use provided expiration or defaults
        var absExp = absoluteExpiration 
            ?? (_options.DefaultAbsoluteExpirationSeconds.HasValue 
                ? TimeSpan.FromSeconds(_options.DefaultAbsoluteExpirationSeconds.Value) 
                : (TimeSpan?)null);

        var slideExp = slidingExpiration 
            ?? (_options.DefaultSlidingExpirationSeconds.HasValue 
                ? TimeSpan.FromSeconds(_options.DefaultSlidingExpirationSeconds.Value) 
                : (TimeSpan?)null);

        if (absExp.HasValue)
        {
            cacheOptions.AbsoluteExpirationRelativeToNow = absExp;
        }

        if (slideExp.HasValue)
        {
            cacheOptions.SlidingExpiration = slideExp;
        }

        _memoryCache.Set(key, value, cacheOptions);
    }

    private DistributedCacheEntryOptions CreateDistributedCacheOptions(
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null)
    {
        var options = new DistributedCacheEntryOptions();

        var absExp = absoluteExpiration 
            ?? (_options.DefaultAbsoluteExpirationSeconds.HasValue 
                ? TimeSpan.FromSeconds(_options.DefaultAbsoluteExpirationSeconds.Value) 
                : (TimeSpan?)null);

        var slideExp = slidingExpiration 
            ?? (_options.DefaultSlidingExpirationSeconds.HasValue 
                ? TimeSpan.FromSeconds(_options.DefaultSlidingExpirationSeconds.Value) 
                : (TimeSpan?)null);

        if (absExp.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = absExp;
        }

        if (slideExp.HasValue)
        {
            options.SlidingExpiration = slideExp;
        }

        return options;
    }

    private bool CheckRedisAvailability()
    {
        if (!_options.EnableL2Cache || _redisConnection == null)
        {
            return false;
        }

        try
        {
            return _redisConnection.IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Redis availability");
            return false;
        }
    }

    private int GetMemoryCacheCount()
    {
        // IMemoryCache doesn't expose count directly
        // This is a limitation - we would need a wrapper to track this
        // For now, return -1 to indicate unknown
        return -1;
    }

    public void Dispose()
    {
        // IConnectionMultiplexer should be managed by DI container
        // Don't dispose it here as it's injected
    }
}
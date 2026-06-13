using System.Collections.Concurrent;
using System.Text;
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
/// Includes stampede protection via per-key locking in GetOrSetAsync.
/// </summary>
public sealed class HybridCacheService : ICacheService, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly IConnectionMultiplexer? _redisConnection;
    private readonly CacheOptions _options;
    private readonly ILogger<HybridCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Stampede protection: per-key semaphores for GetOrSetAsync
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    // Statistics tracking
    private long _totalGets;
    private long _hits;
    private long _misses;

    public HybridCacheService(
        IMemoryCache memoryCache,
        IOptions<CacheOptions> options,
        ILogger<HybridCacheService> logger)
        : this(memoryCache, null, null, options, logger)
    {
    }

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

        if (!IsRedisAvailable && _options.EnableL2Cache)
        {
            _logger.LogWarning("Redis is not available. Cache will operate in L1 (memory) only mode");
        }
    }

    /// <summary>
    /// Dynamically checks Redis availability on each call.
    /// If Redis reconnects after a transient failure, operations resume automatically.
    /// </summary>
    private bool IsRedisAvailable =>
        _options.EnableL2Cache && _redisConnection is { IsConnected: true };

    /// <summary>
    /// Applies the configured key prefix (RedisInstanceName) to ensure
    /// keys don't collide with other applications sharing the same Redis instance.
    /// </summary>
    private string PrefixKey(string key) =>
        string.IsNullOrEmpty(_options.RedisInstanceName) ? key : $"{_options.RedisInstanceName}{key}";

    /// <inheritdoc />
    public async Task<Result<T?>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return Result<T?>.Success(default, "Caching is disabled");

        if (string.IsNullOrWhiteSpace(key))
            return Result<T?>.Failure("Cache key cannot be null or empty", 400);

        try
        {
            if (_options.EnableStatistics)
                Interlocked.Increment(ref _totalGets);

            var prefixedKey = PrefixKey(key);

            // Try L1 (Memory) first
            if (_options.EnableL1Cache && _memoryCache.TryGetValue(prefixedKey, out T? cachedValue))
            {
                if (_options.EnableStatistics)
                    Interlocked.Increment(ref _hits);

                _logger.LogTrace("Cache hit (L1) for key: {Key}", key);
                return Result<T?>.Success(cachedValue);
            }

            // Try L2 (Redis) if available
            if (IsRedisAvailable && _distributedCache != null)
            {
                var redisValue = await _distributedCache.GetStringAsync(prefixedKey, cancellationToken);

                if (!string.IsNullOrEmpty(redisValue))
                {
                    var deserializedValue = JsonSerializer.Deserialize<T>(redisValue, _jsonOptions);

                    // Populate L1 cache
                    if (_options.EnableL1Cache)
                        SetInMemoryCache(prefixedKey, deserializedValue);

                    if (_options.EnableStatistics)
                        Interlocked.Increment(ref _hits);

                    _logger.LogTrace("Cache hit (L2) for key: {Key}", key);
                    return Result<T?>.Success(deserializedValue);
                }
            }

            if (_options.EnableStatistics)
                Interlocked.Increment(ref _misses);

            _logger.LogTrace("Cache miss for key: {Key}", key);
            return Result<T?>.Success(default, "Cache miss");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache key: {Key}", key);

            if (_options.EnableGracefulDegradation)
                return Result<T?>.Success(default, "Cache retrieval failed, returning default");

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
            return Result.Success("Caching is disabled");

        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure("Cache key cannot be null or empty", 400);

        var prefixedKey = PrefixKey(key);

        try
        {
            // Set in L1 (Memory)
            if (_options.EnableL1Cache)
                SetInMemoryCache(prefixedKey, value, absoluteExpiration, slidingExpiration);

            // Set in L2 (Redis)
            if (IsRedisAvailable && _distributedCache != null)
            {
                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = CreateDistributedCacheOptions(absoluteExpiration, slidingExpiration);
                await _distributedCache.SetStringAsync(prefixedKey, jsonValue, options, cancellationToken);
            }

            _logger.LogTrace("Cache set for key: {Key}", key);
            return Result.Success("Cache entry set successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key: {Key}", key);

            // If Redis failed but memory cache succeeded, consider it partial success
            if (_options.EnableGracefulDegradation && _options.EnableL1Cache)
                return Result.Success("Cache entry set in L1 only (L2 unavailable)");

            return Result.Failure($"Cache set failed: {ex.Message}", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure("Cache key cannot be null or empty", 400);

        var prefixedKey = PrefixKey(key);

        try
        {
            if (_options.EnableL1Cache)
                _memoryCache.Remove(prefixedKey);

            if (IsRedisAvailable && _distributedCache != null)
                await _distributedCache.RemoveAsync(prefixedKey, cancellationToken);

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
            return Result<bool>.Failure("Cache key cannot be null or empty", 400);

        var prefixedKey = PrefixKey(key);

        try
        {
            if (_options.EnableL1Cache && _memoryCache.TryGetValue(prefixedKey, out _))
                return Result<bool>.Success(true);

            if (IsRedisAvailable && _redisConnection != null)
            {
                var db = _redisConnection.GetDatabase();
                var exists = await db.KeyExistsAsync(prefixedKey);
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
    /// <remarks>
    /// Uses per-key locking to prevent cache stampede (thundering herd).
    /// Only one caller per key will execute the factory; others wait for the result.
    /// Factory exceptions are returned as failures to preserve Result-based error handling.
    /// </remarks>
    public async Task<Result<T>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result<T>.Failure("Cache key cannot be null or empty", 400);

        if (factory is null)
            return Result<T>.Failure("Factory function cannot be null", 400);

        // Try cache first (no lock needed for reads)
        var getResult = await GetAsync<T>(key, cancellationToken);

        if (getResult is { IsSuccess: true, Data: not null })
            return Result<T>.Success(getResult.Data);

        if (!getResult.IsSuccess && !_options.EnableGracefulDegradation)
            return Result<T>.Failure(getResult.Message ?? "Cache retrieval failed", getResult.StatusCode);

        // Cache miss — acquire per-key lock to prevent stampede
        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await keyLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock (another thread may have populated the cache)
            getResult = await GetAsync<T>(key, cancellationToken);

            if (getResult is { IsSuccess: true, Data: not null })
                return Result<T>.Success(getResult.Data);

            if (!getResult.IsSuccess && !_options.EnableGracefulDegradation)
                return Result<T>.Failure(getResult.Message ?? "Cache retrieval failed", getResult.StatusCode);

            // Execute factory
            _logger.LogTrace("Cache miss, executing factory for key: {Key}", key);
            T value;
            try
            {
                value = await factory(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetOrSet factory failed for key: {Key}", key);
                return Result<T>.Failure($"GetOrSet operation failed: {ex.Message}", 500);
            }

            // Set in cache (fire-and-forget is OK — cache failures don't break the operation)
            var setResult = await SetAsync(key, value, absoluteExpiration, slidingExpiration, cancellationToken);

            if (!setResult.IsSuccess)
                _logger.LogWarning("Failed to cache value for key {Key}: {Message}", key, setResult.Message);

            return Result<T>.Success(value);
        }
        finally
        {
            keyLock.Release();

            // Clean up the semaphore if no one else is waiting
            if (keyLock.CurrentCount == 1)
                _keyLocks.TryRemove(key, out _);
        }
    }

    /// <inheritdoc />
    public async Task<Result<int>> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return Result<int>.Failure("Prefix cannot be null or empty", 400);

        try
        {
            var removedCount = 0;
            var prefixedPattern = PrefixKey(prefix);

            // Note: L1 (MemoryCache) doesn't support pattern-based removal efficiently
            _logger.LogDebug("L1 cache does not support prefix-based removal. Only L2 (Redis) entries will be removed");

            // Remove from L2 (Redis) using SCAN
            if (IsRedisAvailable && _redisConnection != null)
            {
                var db = _redisConnection.GetDatabase();
                var server = _redisConnection.GetServer(_redisConnection.GetEndPoints().First());

                var pattern = $"{prefixedPattern}*";
                var keys = server.Keys(pattern: pattern, pageSize: 1000).ToArray();

                if (keys.Length > 0)
                {
                    await db.KeyDeleteAsync(keys);
                    removedCount = keys.Length;
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
        var stats = new CacheStatistics
        {
            TotalGets = _totalGets,
            Hits = _hits,
            Misses = _misses,
            L1EntryCount = -1, // IMemoryCache doesn't expose count
            L2Available = IsRedisAvailable
        };

        return Result<CacheStatistics>.Success(stats);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uses prefix-based removal instead of FlushDatabase to avoid nuking
    /// other applications' data when sharing a Redis instance.
    /// </remarks>
    public async Task<Result> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("L1 cache clearing is not fully supported by IMemoryCache. Only L2 will be cleared");

            // Clear L2 (Redis) using prefix-based removal — NOT FlushDatabase
            if (IsRedisAvailable && _redisConnection != null)
            {
                var db = _redisConnection.GetDatabase();
                var server = _redisConnection.GetServer(_redisConnection.GetEndPoints().First());

                var prefix = _options.RedisInstanceName;
                if (!string.IsNullOrEmpty(prefix))
                {
                    var keys = server.Keys(pattern: $"{prefix}*", pageSize: 1000).ToArray();
                    if (keys.Length > 0)
                    {
                        await db.KeyDeleteAsync(keys);
                        _logger.LogWarning("L2 (Redis) cleared {Count} keys with prefix '{Prefix}'", keys.Length, prefix);
                    }
                }
                else
                {
                    _logger.LogWarning("No Redis key prefix configured — skipping L2 clear to prevent data loss. " +
                                       "Set CacheOptions.RedisInstanceName to enable ClearAllAsync");
                }
            }

            // Reset statistics
            Interlocked.Exchange(ref _totalGets, 0);
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);

            return Result.Success("Cache cleared successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return Result.Failure($"Cache clear failed: {ex.Message}", 500);
        }
    }

    // Private helpers

    private void SetInMemoryCache<T>(
        string key,
        T? value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null)
    {
        var cacheOptions = new MemoryCacheEntryOptions();

        var absExp = absoluteExpiration
            ?? (_options.DefaultAbsoluteExpirationSeconds.HasValue
                ? TimeSpan.FromSeconds(_options.DefaultAbsoluteExpirationSeconds.Value)
                : null);

        var slideExp = slidingExpiration
            ?? (_options.DefaultSlidingExpirationSeconds.HasValue
                ? TimeSpan.FromSeconds(_options.DefaultSlidingExpirationSeconds.Value)
                : null);

        if (absExp.HasValue)
            cacheOptions.AbsoluteExpirationRelativeToNow = absExp;

        if (slideExp.HasValue)
            cacheOptions.SlidingExpiration = slideExp;

        if (_options.MemoryCacheSizeLimitMb.HasValue)
            cacheOptions.Size = EstimateCacheEntrySize(value);

        _memoryCache.Set(key, value, cacheOptions);
    }

    private long EstimateCacheEntrySize<T>(T? value)
    {
        if (value is null)
            return 1;

        if (value is string stringValue)
            return Math.Max(Encoding.UTF8.GetByteCount(stringValue), 1);

        if (value is byte[] byteArray)
            return Math.Max(byteArray.LongLength, 1);

        try
        {
            return Math.Max(JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions).LongLength, 1);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to estimate memory cache entry size; using minimum size");
            return 1;
        }
    }

    private DistributedCacheEntryOptions CreateDistributedCacheOptions(
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null)
    {
        var options = new DistributedCacheEntryOptions();

        var absExp = absoluteExpiration
            ?? (_options.DefaultAbsoluteExpirationSeconds.HasValue
                ? TimeSpan.FromSeconds(_options.DefaultAbsoluteExpirationSeconds.Value)
                : null);

        var slideExp = slidingExpiration
            ?? (_options.DefaultSlidingExpirationSeconds.HasValue
                ? TimeSpan.FromSeconds(_options.DefaultSlidingExpirationSeconds.Value)
                : null);

        if (absExp.HasValue)
            options.AbsoluteExpirationRelativeToNow = absExp;

        if (slideExp.HasValue)
            options.SlidingExpiration = slideExp;

        return options;
    }

    public void Dispose()
    {
        // Dispose per-key semaphores
        foreach (var kvp in _keyLocks)
            kvp.Value.Dispose();

        _keyLocks.Clear();
    }
}

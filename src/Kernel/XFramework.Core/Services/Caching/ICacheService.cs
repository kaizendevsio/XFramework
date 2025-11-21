using XFramework.Core.Patterns;

namespace XFramework.Core.Services.Caching;

/// <summary>
/// Provides caching operations with hybrid in-memory (L1) and distributed Redis (L2) support.
/// All operations return Result&lt;T&gt; for consistent error handling.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key. Checks L1 (memory) first, then L2 (Redis).
    /// </summary>
    /// <typeparam name="T">The type of the cached value</typeparam>
    /// <param name="key">The cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the cached value, or null if not found</returns>
    Task<Result<T?>> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in cache with optional expiration. Writes to both L1 and L2.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache</typeparam>
    /// <param name="key">The cache key</param>
    /// <param name="value">The value to cache</param>
    /// <param name="absoluteExpiration">Absolute expiration time (optional)</param>
    /// <param name="slidingExpiration">Sliding expiration duration (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> SetAsync<T>(
        string key, 
        T value, 
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from cache by key. Removes from both L1 and L2.
    /// </summary>
    /// <param name="key">The cache key to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in cache (L1 or L2).
    /// </summary>
    /// <param name="key">The cache key to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing true if key exists, false otherwise</returns>
    Task<Result<bool>> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from cache, or sets it using the provided factory function if not found.
    /// Implements the cache-aside pattern.
    /// </summary>
    /// <typeparam name="T">The type of the cached value</typeparam>
    /// <param name="key">The cache key</param>
    /// <param name="factory">Factory function to create the value if not cached</param>
    /// <param name="absoluteExpiration">Absolute expiration time (optional)</param>
    /// <param name="slidingExpiration">Sliding expiration duration (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the cached or newly created value</returns>
    Task<Result<T>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cache entries matching the specified prefix.
    /// Useful for invalidating entire cache sections (e.g., "users:*").
    /// </summary>
    /// <param name="prefix">The key prefix to match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the number of keys removed</returns>
    Task<Result<int>> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics including hit/miss rates and entry counts.
    /// </summary>
    /// <returns>Result containing cache statistics</returns>
    Result<CacheStatistics> GetStatistics();

    /// <summary>
    /// Clears all cache entries from both L1 and L2.
    /// Use with caution - this will remove all cached data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> ClearAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents cache performance statistics.
/// </summary>
public record CacheStatistics
{
    /// <summary>
    /// Total number of cache get operations
    /// </summary>
    public long TotalGets { get; init; }

    /// <summary>
    /// Number of successful cache hits
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// Number of cache misses
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// Cache hit rate as a percentage (0-100)
    /// </summary>
    public double HitRate => TotalGets > 0 ? (double)Hits / TotalGets * 100 : 0;

    /// <summary>
    /// Estimated number of entries in L1 cache
    /// </summary>
    public int L1EntryCount { get; init; }

    /// <summary>
    /// Indicates if Redis (L2) is available
    /// </summary>
    public bool L2Available { get; init; }
}
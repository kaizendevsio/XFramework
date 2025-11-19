using XFramework.Core.Patterns;

namespace XFramework.Core.Services.Caching;

/// <summary>
/// Configuration options for the hybrid caching service.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Caching";

    /// <summary>
    /// Enables or disables the entire caching system
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enables or disables L1 (in-memory) caching
    /// </summary>
    public bool EnableL1Cache { get; set; } = true;

    /// <summary>
    /// Enables or disables L2 (Redis) caching
    /// </summary>
    public bool EnableL2Cache { get; set; } = true;

    /// <summary>
    /// Default absolute expiration time for cache entries (in seconds).
    /// Null means no absolute expiration.
    /// </summary>
    public int? DefaultAbsoluteExpirationSeconds { get; set; } = 3600; // 1 hour

    /// <summary>
    /// Default sliding expiration time for cache entries (in seconds).
    /// Null means no sliding expiration.
    /// </summary>
    public int? DefaultSlidingExpirationSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Maximum size limit for L1 (memory) cache in MB.
    /// Null means no size limit.
    /// </summary>
    public int? MemoryCacheSizeLimitMb { get; set; } = 100;

    /// <summary>
    /// Redis connection string.
    /// Required if EnableL2Cache is true.
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Redis instance name prefix for cache keys.
    /// Useful for separating cache namespaces.
    /// </summary>
    public string RedisInstanceName { get; set; } = "XFramework:";

    /// <summary>
    /// Timeout for Redis operations in milliseconds
    /// </summary>
    public int RedisOperationTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Number of retry attempts for failed Redis operations
    /// </summary>
    public int RedisRetryCount { get; set; } = 3;

    /// <summary>
    /// Enable graceful fallback to L1 only if Redis is unavailable
    /// </summary>
    public bool EnableGracefulDegradation { get; set; } = true;

    /// <summary>
    /// Enable cache statistics tracking
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Validates the configuration options
    /// </summary>
    /// <returns>Validation result with error messages if invalid</returns>
    public Result Validate()
    {
        var errors = new List<string>();

        if (EnableL2Cache && string.IsNullOrWhiteSpace(RedisConnectionString))
        {
            errors.Add("RedisConnectionString is required when EnableL2Cache is true");
        }

        if (MemoryCacheSizeLimitMb.HasValue && MemoryCacheSizeLimitMb.Value <= 0)
        {
            errors.Add("MemoryCacheSizeLimitMb must be greater than 0");
        }

        if (RedisOperationTimeoutMs <= 0)
        {
            errors.Add("RedisOperationTimeoutMs must be greater than 0");
        }

        if (RedisRetryCount < 0)
        {
            errors.Add("RedisRetryCount must be 0 or greater");
        }

        if (errors.Any())
        {
            return Result.Failure(
                $"Cache configuration validation failed: {string.Join("; ", errors)}",
                400);
        }

        return Result.Success("Cache configuration is valid");
    }
}
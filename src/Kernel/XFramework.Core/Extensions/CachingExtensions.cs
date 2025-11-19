using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using XFramework.Core.Services.Caching;

namespace XFramework.Core.Extensions;

/// <summary>
/// Extension methods for configuring caching services.
/// </summary>
public static class CachingExtensions
{
    /// <summary>
    /// Adds hybrid caching services (Memory + Redis) to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <param name="configureOptions">Optional action to configure cache options</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // In Program.cs or Startup.cs
    /// services.AddHybridCaching(configuration);
    /// 
    /// // Or with custom configuration
    /// services.AddHybridCaching(configuration, options =>
    /// {
    ///     options.DefaultAbsoluteExpirationSeconds = 7200; // 2 hours
    ///     options.EnableL2Cache = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddHybridCaching(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CacheOptions>? configureOptions = null)
    {
        // Bind configuration
        var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
        
        // Apply custom configuration if provided
        configureOptions?.Invoke(cacheOptions);

        // Validate configuration
        var validationResult = cacheOptions.Validate();
        if (!validationResult.IsSuccess)
        {
            throw new InvalidOperationException($"Cache configuration is invalid: {validationResult.Message}");
        }

        // Register options
        services.Configure<CacheOptions>(options =>
        {
            options.Enabled = cacheOptions.Enabled;
            options.EnableL1Cache = cacheOptions.EnableL1Cache;
            options.EnableL2Cache = cacheOptions.EnableL2Cache;
            options.DefaultAbsoluteExpirationSeconds = cacheOptions.DefaultAbsoluteExpirationSeconds;
            options.DefaultSlidingExpirationSeconds = cacheOptions.DefaultSlidingExpirationSeconds;
            options.MemoryCacheSizeLimitMb = cacheOptions.MemoryCacheSizeLimitMb;
            options.RedisConnectionString = cacheOptions.RedisConnectionString;
            options.RedisInstanceName = cacheOptions.RedisInstanceName;
            options.RedisOperationTimeoutMs = cacheOptions.RedisOperationTimeoutMs;
            options.RedisRetryCount = cacheOptions.RedisRetryCount;
            options.EnableGracefulDegradation = cacheOptions.EnableGracefulDegradation;
            options.EnableStatistics = cacheOptions.EnableStatistics;
        });

        // Register L1 (Memory) Cache
        if (cacheOptions.EnableL1Cache)
        {
            services.AddMemoryCache(memoryCacheOptions =>
            {
                if (cacheOptions.MemoryCacheSizeLimitMb.HasValue)
                {
                    // Convert MB to bytes
                    memoryCacheOptions.SizeLimit = cacheOptions.MemoryCacheSizeLimitMb.Value * 1024 * 1024;
                }
            });
        }

        // Register L2 (Redis) Cache
        if (cacheOptions.EnableL2Cache && !string.IsNullOrWhiteSpace(cacheOptions.RedisConnectionString))
        {
            try
            {
                // Register Redis connection multiplexer as singleton
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();
                    
                    try
                    {
                        var configOptions = ConfigurationOptions.Parse(cacheOptions.RedisConnectionString!);
                        configOptions.ConnectTimeout = cacheOptions.RedisOperationTimeoutMs;
                        configOptions.SyncTimeout = cacheOptions.RedisOperationTimeoutMs;
                        configOptions.ConnectRetry = cacheOptions.RedisRetryCount;
                        configOptions.AbortOnConnectFail = !cacheOptions.EnableGracefulDegradation;
                        
                        var connection = ConnectionMultiplexer.Connect(configOptions);
                        
                        // Log connection status
                        if (connection.IsConnected)
                        {
                            logger.LogInformation("Successfully connected to Redis at {Endpoints}", 
                                string.Join(", ", connection.GetEndPoints().Select(ep => ep.ToString())));
                        }
                        else
                        {
                            logger.LogWarning("Redis connection created but not connected");
                        }
                        
                        return connection;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to connect to Redis. Caching will fall back to L1 (memory) only");
                        
                        if (!cacheOptions.EnableGracefulDegradation)
                        {
                            throw;
                        }
                        
                        // Return a null object pattern or throw based on configuration
                        throw new InvalidOperationException("Redis connection failed and graceful degradation is enabled", ex);
                    }
                });

                // Register distributed cache (Redis)
                services.AddStackExchangeRedisCache(redisOptions =>
                {
                    redisOptions.Configuration = cacheOptions.RedisConnectionString;
                    redisOptions.InstanceName = cacheOptions.RedisInstanceName;
                });
            }
            catch (Exception ex)
            {
                if (!cacheOptions.EnableGracefulDegradation)
                {
                    throw new InvalidOperationException("Failed to configure Redis cache", ex);
                }
                
                // Log warning and continue without Redis
                using var scope = services.BuildServiceProvider().CreateScope();
                var logger = scope.ServiceProvider.GetService<ILogger<IConnectionMultiplexer>>();
                logger?.LogWarning(ex, "Failed to configure Redis. Caching will operate in L1 (memory) only mode");
            }
        }

        // Register the hybrid cache service
        services.AddSingleton<ICacheService, HybridCacheService>();

        return services;
    }

    /// <summary>
    /// Adds hybrid caching services with minimal configuration (memory only by default).
    /// Useful for development or when Redis is not available.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMemoryCaching(this IServiceCollection services)
    {
        services.Configure<CacheOptions>(options =>
        {
            options.Enabled = true;
            options.EnableL1Cache = true;
            options.EnableL2Cache = false;
            options.EnableStatistics = true;
        });

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, HybridCacheService>();

        return services;
    }

    /// <summary>
    /// Validates cache configuration at startup.
    /// Throws an exception if configuration is invalid.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection ValidateCacheConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
        var validationResult = cacheOptions.Validate();
        
        if (!validationResult.IsSuccess)
        {
            throw new InvalidOperationException($"Cache configuration validation failed: {validationResult.Message}");
        }

        return services;
    }
}
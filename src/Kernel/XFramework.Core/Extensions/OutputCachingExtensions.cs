using Microsoft.AspNetCore.OutputCaching;

namespace XFramework.Core.Extensions;

/// <summary>
/// Extension methods for configuring ASP.NET Core output caching.
/// Output caching stores entire HTTP responses for improved performance on GET requests.
/// </summary>
/// <remarks>
/// Output caching is different from application-level caching:
/// - Output Caching: Caches complete HTTP responses (headers + body)
/// - Application Caching (HybridCacheService): Caches business objects/data in service layer
/// 
/// Use output caching for:
/// - Read-only GET endpoints
/// - Static or semi-static content
/// - Responses that don't vary by user (or vary in predictable ways)
/// 
/// Don't use output caching for:
/// - POST/PUT/DELETE requests
/// - User-specific data (unless using VaryByHeader with Authorization)
/// - Real-time data
/// </remarks>
public static class OutputCachingExtensions
{
    /// <summary>
    /// Adds output caching services with predefined policies.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // In Program.cs or Startup
    /// services.AddOutputCachingWithPolicies();
    /// 
    /// // Then in your endpoints:
    /// app.MapGet("/api/products", GetProducts)
    ///    .CacheOutput("ProductsPolicy");
    /// </code>
    /// </example>
    public static IServiceCollection AddOutputCachingWithPolicies(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            // Default policy - 60 seconds
            options.AddBasePolicy(builder => builder
                .Expire(TimeSpan.FromSeconds(60))
                .Tag("default"));

            // ProductsPolicy - Cache for 10 minutes, vary by query parameters
            // Use for: Product listings, catalog endpoints
            options.AddPolicy("ProductsPolicy", builder => builder
                .Expire(TimeSpan.FromMinutes(10))
                .SetVaryByQuery("page", "pageSize", "search", "categoryId", "sort")
                .SetVaryByHeader("Accept-Language") // Support i18n
                .Tag("products")
                .SetLocking(true)); // Prevent cache stampede

            // UsersPolicy - Cache for 5 minutes, vary by user ID
            // Use for: User profiles, user-specific data
            options.AddPolicy("UsersPolicy", builder => builder
                .Expire(TimeSpan.FromMinutes(5))
                .SetVaryByQuery("id", "userId")
                .SetVaryByHeader("Authorization") // Vary by authenticated user
                .Tag("users")
                .SetLocking(true));

            // StaticContentPolicy - Cache for 1 hour, vary by route
            // Use for: Static pages, configuration endpoints, metadata
            options.AddPolicy("StaticContentPolicy", builder => builder
                .Expire(TimeSpan.FromHours(1))
                .SetVaryByRouteValue("id", "slug")
                .SetVaryByHeader("Accept-Language")
                .Tag("static")
                .SetLocking(false)); // Static content doesn't need locking

            // ShortLivedPolicy - Cache for 30 seconds
            // Use for: Frequently changing data, dashboards, analytics
            options.AddPolicy("ShortLivedPolicy", builder => builder
                .Expire(TimeSpan.FromSeconds(30))
                .SetVaryByQuery("*") // Vary by all query parameters
                .Tag("short-lived"));

            // ApiListPolicy - Cache for 2 minutes for list endpoints
            // Use for: Generic list/collection endpoints
            options.AddPolicy("ApiListPolicy", builder => builder
                .Expire(TimeSpan.FromMinutes(2))
                .SetVaryByQuery("page", "pageSize", "filter", "orderBy")
                .SetVaryByHeader("Accept", "Accept-Language")
                .Tag("api-list")
                .SetLocking(true));
        });

        return services;
    }

    /// <summary>
    /// Configures output caching middleware in the request pipeline.
    /// Must be called AFTER UseResponseCompression() and BEFORE UseAuthorization().
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseConfiguredOutputCaching(this IApplicationBuilder app)
    {
        app.UseOutputCache();
        return app;
    }

    /// <summary>
    /// Invalidates cache entries by tag.
    /// </summary>
    /// <param name="app">The application</param>
    /// <param name="tag">The tag to invalidate</param>
    /// <example>
    /// <code>
    /// // After updating products
    /// app.InvalidateCacheByTag("products");
    /// </code>
    /// </example>
    public static async Task InvalidateCacheByTag(this WebApplication app, string tag)
    {
        var cache = app.Services.GetRequiredService<IOutputCacheStore>();
        await cache.EvictByTagAsync(tag, default);
    }

    /// <summary>
    /// Invalidates cache entries by multiple tags.
    /// </summary>
    public static async Task InvalidateCacheByTags(this WebApplication app, params string[] tags)
    {
        var cache = app.Services.GetRequiredService<IOutputCacheStore>();
        foreach (var tag in tags)
        {
            await cache.EvictByTagAsync(tag, default);
        }
    }
}
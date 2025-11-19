# XFramework Caching Strategy

## Overview

XFramework implements a comprehensive multi-layer caching strategy to optimize performance and reduce database load. This document outlines when and how to use different caching mechanisms.

## Caching Layers

### 1. Output Caching (HTTP Response Cache)

**What**: Caches complete HTTP responses (headers + body)  
**Where**: ASP.NET Core middleware layer  
**Technology**: .NET 9 Output Caching middleware  
**Storage**: In-memory or Redis (configurable)

#### When to Use Output Caching

✅ **Good Candidates:**
- GET endpoints with static or semi-static data
- Product listings and catalog endpoints
- User profile endpoints (when using vary-by-header with Authorization)
- Static content endpoints (configuration, metadata)
- API endpoints with high read/write ratio (>90% reads)

❌ **Avoid For:**
- POST/PUT/DELETE requests (not supported)
- Real-time data that changes frequently
- User-specific data without proper vary-by-header configuration
- Responses with authentication-sensitive data without proper cache policies

#### Available Cache Policies

```csharp
// ProductsPolicy - Cache for 10 minutes
app.MapGet("/api/products", GetProducts)
   .CacheOutput("ProductsPolicy");

// UsersPolicy - Cache for 5 minutes (varies by Authorization header)
app.MapGet("/api/users/{id}", GetUser)
   .CacheOutput("UsersPolicy");

// StaticContentPolicy - Cache for 1 hour
app.MapGet("/api/config", GetConfig)
   .CacheOutput("StaticContentPolicy");

// ShortLivedPolicy - Cache for 30 seconds
app.MapGet("/api/dashboard/stats", GetStats)
   .CacheOutput("ShortLivedPolicy");

// ApiListPolicy - Cache for 2 minutes (generic lists)
app.MapGet("/api/items", GetItems)
   .CacheOutput("ApiListPolicy");
```

### 2. Application Caching (Business Object Cache)

**What**: Caches business objects, domain entities, and computed data  
**Where**: Service layer (business logic)  
**Technology**: HybridCacheService (L1: Memory, L2: Redis)  
**Storage**: Dual-layer (Memory + Redis with graceful fallback)

#### When to Use Application Caching

✅ **Good Candidates:**
- Database query results
- Complex computed values
- Frequently accessed reference data (countries, categories)
- Session data
- User preferences
- External API responses

❌ **Avoid For:**
- Data that changes on every request
- Very large objects (>1MB per item)
- Data with complex invalidation requirements

#### Using HybridCacheService

```csharp
public class ProductService
{
    private readonly ICacheService _cache;
    private readonly IProductRepository _repository;

    public async Task<Result<Product>> GetProductAsync(int id)
    {
        var cacheKey = $"product:{id}";
        
        // Try to get from cache first
        var cachedResult = await _cache.GetAsync<Product>(cacheKey);
        if (cachedResult.IsSuccess && cachedResult.Data != null)
        {
            return cachedResult;
        }

        // Cache miss - fetch from database
        var product = await _repository.GetByIdAsync(id);
        
        // Store in cache for 10 minutes
        await _cache.SetAsync(cacheKey, product, TimeSpan.FromMinutes(10));
        
        return Result<Product>.Success(product);
    }

    // Or use GetOrSetAsync pattern
    public async Task<Result<Product>> GetProductAsync(int id)
    {
        var cacheKey = $"product:{id}";
        
        return await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await _repository.GetByIdAsync(id),
            absoluteExpiration: TimeSpan.FromMinutes(10)
        );
    }
}
```

## Cache Invalidation Strategies

### Output Cache Invalidation

Use cache tags for efficient invalidation:

```csharp
// After updating products
await app.InvalidateCacheByTag("products");

// After updating multiple resource types
await app.InvalidateCacheByTags("products", "users");
```

### Application Cache Invalidation

```csharp
// Remove specific item
await _cache.RemoveAsync($"product:{id}");

// Remove by prefix (all products)
await _cache.RemoveByPrefixAsync("product:");

// Clear entire cache (use sparingly!)
await _cache.ClearAllAsync();
```

## Decision Tree

```
START: Do I need to cache this data?
├── Is it an HTTP GET endpoint?
│   ├── YES
│   │   ├── Data changes < once per minute?
│   │   │   ├── YES → Use Output Caching with appropriate policy
│   │   │   └── NO → Consider short-lived Output Cache or Application Cache
│   │   └── Data is user-specific?
│   │       ├── YES → Use Output Caching with VaryByHeader("Authorization")
│   │       └── NO → Use Output Caching
│   └── NO
│       ├── Is it expensive to compute/fetch?
│       │   ├── YES → Use Application Caching (HybridCacheService)
│       │   └── NO → Don't cache
│       └── Is it accessed frequently (>100 req/min)?
│           ├── YES → Use Application Caching
│           └── NO → Don't cache
```

## Response Compression

All HTTP responses are automatically compressed using:
- **Brotli** (preferred): Better compression ratio, supported by modern browsers
- **Gzip** (fallback): Universal compatibility

Compression is applied BEFORE output caching, so cached responses are already compressed.

### Compressed MIME Types
- JSON: `application/json`
- XML: `application/xml`, `text/xml`
- HTML: `text/html`
- CSS: `text/css`
- JavaScript: `text/javascript`, `application/javascript`
- Fonts: `font/woff`, `font/woff2`
- SVG: `image/svg+xml`

## Middleware Order

The middleware pipeline order is critical for proper caching and compression:

```csharp
1. Exception handling
2. HTTPS redirection
3. Custom middleware (headers)
4. CORS
5. Routing
6. Authentication
7. Authorization
8. Response compression  ← Compress BEFORE caching
9. Output caching        ← Cache compressed responses
10. Endpoints
```

## Health Checks

### Available Endpoints

- **`/health`**: Detailed health status of all checks
  - Returns: JSON with all health check results
  - Use for: Monitoring dashboards, alerts

- **`/health/live`**: Liveness probe (is the app running?)
  - Returns: Simple JSON indicating app is alive
  - Use for: Kubernetes/orchestrator liveness probes

- **`/health/ready`**: Readiness probe (can the app serve traffic?)
  - Returns: JSON with infrastructure checks (Redis, DB, etc.)
  - Use for: Kubernetes/orchestrator readiness probes, load balancer decisions

### Health Check Statuses

- **Healthy**: Everything is working correctly
- **Degraded**: Some components are slow or partially unavailable (e.g., Redis down but app continues with memory-only cache)
- **Unhealthy**: Critical components are down

## Performance Considerations

### Cache Key Design

Use structured, hierarchical keys:

```csharp
// Good
"product:123"
"user:456:profile"
"catalog:electronics:page:1"

// Bad
"prod123"
"u456"
"data"
```

### Expiration Guidelines

| Data Type | Recommended TTL | Policy |
|-----------|----------------|---------|
| Static content | 1 hour | StaticContentPolicy |
| Product catalogs | 10 minutes | ProductsPolicy |
| User profiles | 5 minutes | UsersPolicy |
| Dashboard stats | 30 seconds | ShortLivedPolicy |
| List endpoints | 2 minutes | ApiListPolicy |

### Memory Limits

- **L1 Cache (Memory)**: Configurable size limit (default: depends on configuration)
- **L2 Cache (Redis)**: Virtually unlimited, but monitor Redis memory usage

## Monitoring

Monitor cache effectiveness through:

1. **Cache Hit Ratio**: Available via `ICacheService.GetStatistics()`
2. **Redis Health**: Check `/health` endpoint
3. **Response Times**: Compare cached vs uncached endpoints
4. **Memory Usage**: Monitor application memory footprint

## Best Practices

1. ✅ **Always set expiration times** - Prevents stale data
2. ✅ **Use cache tags** - Enables efficient invalidation
3. ✅ **Vary by user when needed** - Use Authorization header for user-specific data
4. ✅ **Monitor cache hit rates** - Optimize cache keys and TTLs
5. ✅ **Handle cache misses gracefully** - Always have fallback logic
6. ✅ **Document cache policies** - Make it clear what's cached and why
7. ❌ **Don't cache sensitive data** - Unless properly secured with vary-by-header
8. ❌ **Don't cache very large objects** - Use streaming or pagination instead
9. ❌ **Don't set infinite TTLs** - Always expire cache entries

## Configuration

### appsettings.json

```json
{
  "CacheOptions": {
    "Enabled": true,
    "EnableL1Cache": true,
    "EnableL2Cache": true,
    "DefaultAbsoluteExpirationSeconds": 600,
    "DefaultSlidingExpirationSeconds": 300,
    "MemoryCacheSizeLimitMb": 512,
    "RedisConnectionString": "localhost:6379",
    "RedisInstanceName": "xframework:",
    "RedisOperationTimeoutMs": 5000,
    "RedisRetryCount": 3,
    "EnableGracefulDegradation": true,
    "EnableStatistics": true
  }
}
```

## Troubleshooting

### Cache Not Working

1. Check if caching is enabled in configuration
2. Verify Redis connection (check `/health` endpoint)
3. Ensure cache keys are correct
4. Check cache expiration times

### Stale Data

1. Review cache TTL settings
2. Implement proper cache invalidation
3. Use cache tags for easier invalidation
4. Consider shorter TTLs for frequently changing data

### High Memory Usage

1. Review L1 cache size limits
2. Reduce cache TTLs
3. Implement cache eviction policies
4. Consider moving more data to L2 (Redis)

## Future Enhancements

Planned improvements:
- Cache warming on application startup
- Distributed cache invalidation via pub/sub
- Cache analytics and reporting
- Automatic cache key generation
- Smart TTL adjustment based on access patterns
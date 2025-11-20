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

### Overview

All HTTP responses are automatically compressed using a two-tier compression strategy:
- **Brotli** (primary): Better compression ratio (typically 60-80% for JSON), supported by modern browsers
- **Gzip** (fallback): Universal compatibility (typically 50-70% for JSON)

Compression is applied BEFORE output caching, so cached responses are already compressed. This ensures optimal bandwidth usage and faster response times.

### Configuration (Phase 1.4 Implementation)

**Implementation Files:**
- Configuration: [`src/Kernel/XFramework.Core/Extensions/ResponseCompressionExtensions.cs`](../src/Kernel/XFramework.Core/Extensions/ResponseCompressionExtensions.cs)
- Pipeline Integration: [`src/Kernel/XFramework.Core/Extensions/XApplication.cs`](../src/Kernel/XFramework.Core/Extensions/XApplication.cs)

**Verified Settings (Phase 3.4 Verification - November 2025):**
- ✅ **Brotli Provider**: Configured as primary with `CompressionLevel.Optimal`
- ✅ **Gzip Provider**: Configured as fallback with `CompressionLevel.Optimal`
- ✅ **HTTPS Enabled**: `EnableForHttps = true` (safe for API responses)
- ✅ **Middleware Order**: Compression → Output Caching (correct order)
- ✅ **Comprehensive MIME Types**: JSON, XML, HTML, CSS, JS, fonts, SVG

**Note on Compression Level:**
- Current: `CompressionLevel.Optimal` - provides excellent balance between compression ratio and CPU usage
- Alternative: `CompressionLevel.Fastest` - prioritizes speed over compression ratio
- Alternative: `CompressionLevel.SmallestSize` - maximum compression, higher CPU cost

### Compressed MIME Types

The following content types are automatically compressed:

**API Responses:**
- JSON: `application/json`, `application/json; charset=utf-8`
- XML: `application/xml`, `text/xml`

**Web Content:**
- HTML: `text/html`
- CSS: `text/css`
- JavaScript: `text/javascript`, `application/javascript`, `text/plain`

**Fonts:**
- WOFF: `font/woff`, `font/woff2`, `application/font-woff`, `application/font-woff2`
- TrueType: `application/x-font-ttf`, `application/x-font-opentype`
- EOT: `application/vnd.ms-fontobject`

**Graphics:**
- SVG: `image/svg+xml`

**Excluded Types:**
- Images (JPEG, PNG, GIF) - already compressed
- Videos - already compressed
- Other binary formats with built-in compression

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

### Expected Compression Ratios

Based on content type and compression algorithm:

| Content Type | Brotli Compression | Gzip Compression | Notes |
|-------------|-------------------|------------------|-------|
| JSON (API responses) | 60-80% reduction | 50-70% reduction | Highly compressible due to text nature |
| XML | 65-75% reduction | 55-70% reduction | Similar to JSON |
| HTML | 70-80% reduction | 60-75% reduction | Very compressible |
| CSS | 65-75% reduction | 55-65% reduction | Good compression |
| JavaScript | 60-70% reduction | 50-65% reduction | Text-based, compresses well |
| SVG | 70-85% reduction | 60-75% reduction | XML-based, excellent compression |

**Example:** A 10KB JSON response typically compresses to:
- Brotli: ~2-4KB (60-80% savings)
- Gzip: ~3-5KB (50-70% savings)

### Testing Compression

#### Prerequisites
- Ensure the API service is running (e.g., `http://localhost:5106` for Inventario.Api)
- Use curl or PowerShell to test with different Accept-Encoding headers

#### Using curl (Git Bash/WSL)

```bash
# Test without compression (baseline)
curl -H "Accept-Encoding: identity" http://localhost:5106/api/products -i

# Test with Brotli compression
curl -H "Accept-Encoding: br" http://localhost:5106/api/products -i --compressed

# Test with Gzip compression
curl -H "Accept-Encoding: gzip" http://localhost:5106/api/products -i --compressed

# Measure compressed size
curl -H "Accept-Encoding: br" http://localhost:5106/api/products \
  -w "\nSize: %{size_download} bytes\n" -o /dev/null -s
```

#### Using PowerShell (Windows)

```powershell
# Test with compression headers
$headers = @{ "Accept-Encoding" = "br,gzip" }
$response = Invoke-WebRequest -Uri "http://localhost:5106/api/products" -Headers $headers -Method GET

# Check Content-Encoding header (should be 'br' or 'gzip')
$response.Headers["Content-Encoding"]

# Check content length
$response.RawContentLength
```

#### Expected Response Headers

When compression is working correctly:

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Content-Encoding: br                    # or 'gzip' if client doesn't support Brotli
Vary: Accept-Encoding                   # Important for caching
Content-Length: 2450                    # Compressed size
```

### Compression Best Practices

1. ✅ **Always serve over HTTPS** - Compression over HTTP can have security implications
2. ✅ **Let middleware handle it** - Don't manually compress responses in code
3. ✅ **Verify Vary header** - Ensures proper caching with/without compression
4. ✅ **Monitor compression ratio** - Track bandwidth savings vs CPU usage
5. ✅ **Test with real clients** - Verify browsers and API clients support compression
6. ❌ **Don't compress already-compressed content** - Images, videos, etc.
7. ❌ **Don't compress very small responses** - Overhead not worth it (< 1KB)

### Troubleshooting Compression

#### Compression Not Applied

**Symptoms:** `Content-Encoding` header missing, response size unchanged

**Possible Causes:**
1. Client didn't send `Accept-Encoding` header
2. Response content-type not in configured MIME types list
3. Response too small (< 1KB threshold)
4. Response already compressed (e.g., image)
5. Middleware not enabled or in wrong order

**Solutions:**
1. Verify client sends `Accept-Encoding: br, gzip` header
2. Check content-type matches configured MIME types in `ResponseCompressionExtensions.cs`
3. Test with larger responses (>1KB)
4. Review middleware pipeline order in `XApplication.cs`

#### Wrong Compression Algorithm

**Symptoms:** Getting Gzip when expecting Brotli

**Possible Causes:**
1. Client doesn't support Brotli (`Accept-Encoding` doesn't include `br`)
2. Brotli provider not configured correctly

**Solutions:**
1. Verify client supports Brotli (modern browsers do)
2. Check providers list in `AddConfiguredResponseCompression()` method

#### High CPU Usage

**Symptoms:** Server CPU spikes during compression

**Possible Causes:**
1. Compression level too high (`SmallestSize`)
2. Compressing large responses frequently

**Solutions:**
1. Consider using `CompressionLevel.Fastest` instead of `Optimal`
2. Add response size limits or selective compression
3. Monitor and benchmark different compression levels

### Phase 3.4 Verification Results (November 2025)

**Configuration Review:** ✅ **PASSED**
- Brotli and Gzip providers correctly configured
- Optimal compression level set for both providers
- HTTPS compression enabled (appropriate for API)
- MIME types comprehensive and correct
- Middleware pipeline order correct (compression → caching)

**Runtime Testing:** ⚠️ **BLOCKED**
- Unable to test live endpoints due to service startup issues (DI configuration)
- Recommendation: Resolve `ICacheService` dependency injection issue before runtime testing
- Testing scripts provided above for future verification

**Expected Performance:**
- JSON responses: 60-80% bandwidth reduction with Brotli
- No significant latency impact with `CompressionLevel.Optimal`
- Automatic fallback to Gzip for older clients

**Recommendations:**
1. Once service starts successfully, test compression on `/api/products` endpoint
2. Measure actual compression ratios to validate expectations
3. Monitor CPU usage to ensure `CompressionLevel.Optimal` is appropriate
4. Consider adding compression metrics to monitoring dashboard

## Future Enhancements

Planned improvements:
- Cache warming on application startup
- Distributed cache invalidation via pub/sub
- Cache analytics and reporting
- Automatic cache key generation
- Smart TTL adjustment based on access patterns
- Compression ratio monitoring and alerting
- Adaptive compression level based on server load
# Caching Strategy Guide - XFramework

## Overview

XFramework implements a sophisticated multi-tier caching strategy to optimize performance and reduce database load. This guide focuses on application-level caching using `HybridCacheService` (dual-layer: Memory + Redis) and how to integrate it into VSA services.

## Table of Contents

1. [Caching Architecture](#caching-architecture)
2. [HybridCacheService Overview](#hybridcacheservice-overview)
3. [Cache Key Management](#cache-key-management)
4. [Cache Invalidation Patterns](#cache-invalidation-patterns)
5. [Service Integration](#service-integration)
6. [Performance Considerations](#performance-considerations)
7. [Configuration](#configuration)
8. [Testing Caching Behavior](#testing-caching-behavior)

---

## Caching Architecture

### Multi-Layer Caching Strategy

XFramework uses a comprehensive caching approach across different layers:

```
┌─────────────────────────────────────────┐
│     HTTP Response (Output Cache)        │ ← Middleware layer (see docs/caching-strategy.md)
├─────────────────────────────────────────┤
│  Application Cache (HybridCacheService)  │ ← THIS GUIDE
│    ├─ L1: Memory Cache (Fast)           │
│    └─ L2: Redis Cache (Distributed)     │
├─────────────────────────────────────────┤
│     Database Query Cache (EF Core)       │
└─────────────────────────────────────────┘
```

### When to Use Each Layer

| Layer | Use Case | TTL | Scope |
|-------|----------|-----|-------|
| **Output Cache** | Full HTTP responses (GET endpoints) | 30s - 60m | Per-endpoint |
| **Application Cache** | Business objects, query results, computed data | 5m - 60m | Cross-service |
| **EF Query Cache** | Compiled queries, metadata | N/A | Per-DbContext |

**This guide focuses on Application Cache (L1+L2).**

---

## HybridCacheService Overview

### Architecture

`HybridCacheService` provides two-tier caching with automatic fallback:

```csharp
┌──────────────────────────────────────┐
│       Service/Controller             │
│              ↓                        │
│      HybridCacheService              │
│         ↓         ↓                   │
│    [L1 Cache]  [L2 Cache]            │
│   (MemoryCache) (Redis)              │
│         ↓         ↓                   │
│    Local RAM   Distributed           │
└──────────────────────────────────────┘
```

### Features

✅ **Dual-Layer**: Memory (L1) for speed, Redis (L2) for distribution  
✅ **Automatic Fallback**: If Redis fails, continues with memory-only  
✅ **Type-Safe**: Generic methods with strong typing  
✅ **Graceful Degradation**: Application continues even if cache is down  
✅ **Prefix-Based Invalidation**: Clear related cache entries easily  

### Core Interface

```csharp
public interface ICacheService
{
    // Get cached value
    Task<Result<T>> GetAsync<T>(string key, CancellationToken ct = default);

    // Set cached value with TTL
    Task<Result> SetAsync<T>(
        string key, 
        T value, 
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken ct = default);

    // Get or set pattern (cache-aside)
    Task<Result<T>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken ct = default);

    // Remove single entry
    Task<Result> RemoveAsync(string key, CancellationToken ct = default);

    // Remove by prefix (e.g., "product:*")
    Task<Result> RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    // Clear all cache
    Task<Result> ClearAllAsync(CancellationToken ct = default);
}
```

---

## Cache Key Management

### Naming Conventions

Use hierarchical, descriptive keys:

```csharp
// ✅ Good: Hierarchical, descriptive
"product:123"
"user:456:profile"
"order:789:items"
"catalog:electronics:page:2"
"wallet:abc-def:balance"

// ❌ Bad: Flat, unclear
"p123"
"u456"
"data"
```

### Standard Key Patterns

| Pattern | Example | Use Case |
|---------|---------|----------|
| `{entity}:{id}` | `product:123` | Single entity by ID |
| `{entity}:{filter}:{value}` | `product:category:electronics` | Filtered collections |
| `{entity}:{id}:{property}` | `wallet:abc:balance` | Specific property |
| `{entity}:list:{filter}:{page}` | `product:list:active:1` | Paginated lists |
| `{module}:{entity}:{id}` | `wallets:transaction:xyz` | Multi-module projects |

### Key Generation Helpers

```csharp
public static class CacheKeys
{
    // Entity keys
    public static string Product(Guid id) => $"product:{id}";
    public static string ProductList(int page) => $"product:list:page:{page}";
    public static string ProductByCategory(Guid categoryId) => $"product:category:{categoryId}";

    // User-specific keys
    public static string UserProfile(Guid userId) => $"user:{userId}:profile";
    public static string UserPermissions(Guid userId) => $"user:{userId}:permissions";

    // Wallet keys
    public static string Wallet(Guid walletId) => $"wallet:{walletId}";
    public static string WalletBalance(Guid walletId) => $"wallet:{walletId}:balance";
    public static string WalletTransactions(Guid walletId, int page) => 
        $"wallet:{walletId}:transactions:page:{page}";

    // Computed/aggregate keys
    public static string DailySalesReport(DateTime date) => 
        $"report:sales:daily:{date:yyyyMMdd}";
}
```

### Using Key Helpers

```csharp
public class ProductService
{
    private readonly ICacheService _cache;

    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.Product(id); // "product:{id}"
        
        return await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await FetchFromDatabaseAsync(id, ct),
            absoluteExpiration: TimeSpan.FromMinutes(10),
            cancellationToken: ct);
    }
}
```

---

## Cache Invalidation Patterns

### Pattern 1: Single Item Invalidation

When updating a specific entity:

```csharp
public async Task<Result<Product>> UpdateAsync(
    Guid id,
    UpdateProductRequest request,
    CancellationToken ct = default)
{
    var product = await _db.Products.FindAsync(id);
    if (product == null)
        return Result<Product>.NotFound();

    // Update entity
    product.Name = request.Name;
    product.Price = request.Price;
    await _db.SaveChangesAsync(ct);

    // Invalidate specific item cache
    await _cache.RemoveAsync(CacheKeys.Product(id), ct);

    return Result<Product>.Success(product);
}
```

### Pattern 2: Prefix-Based Invalidation

When changes affect multiple related cache entries:

```csharp
public async Task<Result<Product>> CreateAsync(
    CreateProductRequest request,
    CancellationToken ct = default)
{
    var product = new Product { /* ... */ };
    _db.Products.Add(product);
    await _db.SaveChangesAsync(ct);

    // Invalidate all product lists (pagination, filters, etc.)
    await _cache.RemoveByPrefixAsync("product:list:", ct);
    
    // Also invalidate category-specific lists
    await _cache.RemoveByPrefixAsync($"product:category:{product.CategoryId}", ct);

    return Result<Product>.Success(product, 201);
}
```

### Pattern 3: Multi-Entity Invalidation

When an operation affects multiple entity types:

```csharp
public async Task<Result> TransferInventoryAsync(
    TransferRequest request,
    CancellationToken ct = default)
{
    // Perform transfer logic
    await ExecuteTransferAsync(request, ct);

    // Invalidate all affected caches
    await Task.WhenAll(
        // Source warehouse
        _cache.RemoveByPrefixAsync($"warehouse:{request.SourceWarehouseId}:", ct),
        
        // Destination warehouse
        _cache.RemoveByPrefixAsync($"warehouse:{request.DestinationWarehouseId}:", ct),
        
        // Product inventory
        _cache.RemoveByPrefixAsync($"product:{request.ProductId}:inventory", ct),
        
        // Global inventory lists
        _cache.RemoveByPrefixAsync("inventory:list:", ct)
    );

    return Result.Success();
}
```

### Pattern 4: Selective Invalidation

Invalidate only what changed:

```csharp
public async Task<Result> UpdateWalletBalanceAsync(
    Guid walletId,
    decimal amount,
    CancellationToken ct = default)
{
    var wallet = await _db.Wallets.FindAsync(walletId);
    wallet.Balance += amount;
    await _db.SaveChangesAsync(ct);

    // Invalidate only balance-related caches
    await _cache.RemoveAsync(CacheKeys.WalletBalance(walletId), ct);
    
    // Keep other wallet data (profile, settings, etc.) cached
    // Don't invalidate: CacheKeys.Wallet(walletId)
    // Don't invalidate: CacheKeys.WalletTransactions(walletId, *)

    return Result.Success();
}
```

### Pattern 5: Time-Based Auto-Expiry

Let cache expire naturally for less critical data:

```csharp
// Cache with short TTL - no manual invalidation needed
public async Task<Result<DashboardStats>> GetDashboardStatsAsync(CancellationToken ct = default)
{
    return await _cache.GetOrSetAsync(
        "dashboard:stats",
        async ct => await ComputeStatsAsync(ct),
        absoluteExpiration: TimeSpan.FromSeconds(30), // Auto-expires every 30s
        cancellationToken: ct);
}
```

---

## Service Integration

### Pattern 1: Basic Get with Cache

```csharp
public class ProductService
{
    private readonly DbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<ProductService> _logger;

    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.Product(id);

        // Try cache first
        var cached = await _cache.GetAsync<Product>(cacheKey, ct);
        if (cached.IsSuccess && cached.Data != null)
        {
            _logger.CacheHit(cacheKey);
            return Result<Product>.Success(cached.Data);
        }

        _logger.CacheMiss(cacheKey);

        // Fetch from database
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (product == null)
            return Result<Product>.NotFound();

        // Cache for 10 minutes
        await _cache.SetAsync(cacheKey, product, 
            absoluteExpiration: TimeSpan.FromMinutes(10), 
            cancellationToken: ct);

        return Result<Product>.Success(product);
    }
}
```

### Pattern 2: GetOrSet Pattern (Recommended)

Simplifies cache-aside pattern:

```csharp
public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    var cacheKey = CacheKeys.Product(id);

    var result = await _cache.GetOrSetAsync(
        cacheKey,
        async ct =>
        {
            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (product == null)
                throw new KeyNotFoundException($"Product {id} not found");

            return product;
        },
        absoluteExpiration: TimeSpan.FromMinutes(10),
        cancellationToken: ct);

    if (!result.IsSuccess)
        return Result<Product>.NotFound();

    return Result<Product>.Success(result.Data);
}
```

### Pattern 3: List Caching with Pagination

```csharp
public async Task<Result<List<Product>>> GetListAsync(
    int page,
    int pageSize,
    CancellationToken ct = default)
{
    var cacheKey = CacheKeys.ProductList(page);

    return await _cache.GetOrSetAsync(
        cacheKey,
        async ct =>
        {
            var products = await _db.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return products;
        },
        absoluteExpiration: TimeSpan.FromMinutes(5),
        cancellationToken: ct);
}
```

### Pattern 4: Computed Data Caching

Cache expensive calculations:

```csharp
public async Task<Result<SalesReport>> GetMonthlySalesReportAsync(
    int year,
    int month,
    CancellationToken ct = default)
{
    var cacheKey = $"report:sales:monthly:{year:0000}{month:00}";

    return await _cache.GetOrSetAsync(
        cacheKey,
        async ct =>
        {
            // Expensive computation
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var totalSales = await _db.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate < endDate)
                .SumAsync(o => o.TotalAmount, ct);

            var orderCount = await _db.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate < endDate)
                .CountAsync(ct);

            return new SalesReport
            {
                Year = year,
                Month = month,
                TotalSales = totalSales,
                OrderCount = orderCount,
                AverageOrderValue = orderCount > 0 ? totalSales / orderCount : 0
            };
        },
        absoluteExpiration: TimeSpan.FromHours(1), // Reports don't change often
        cancellationToken: ct);
}
```

### Pattern 5: Conditional Caching

Cache based on conditions:

```csharp
public async Task<Result<Product>> GetByIdAsync(
    Guid id,
    bool includeDeleted = false,
    CancellationToken ct = default)
{
    // Don't cache if including deleted items (admin view)
    if (includeDeleted)
    {
        var product = await _db.Products.FindAsync(id);
        return product != null
            ? Result<Product>.Success(product)
            : Result<Product>.NotFound();
    }

    // Use cache for normal requests
    var cacheKey = CacheKeys.Product(id);
    return await _cache.GetOrSetAsync(
        cacheKey,
        async ct => await FetchActiveProductAsync(id, ct),
        absoluteExpiration: TimeSpan.FromMinutes(10),
        cancellationToken: ct);
}
```

---

## Performance Considerations

### Cache Key Size

```csharp
// ✅ Good: Concise keys
"product:123"                        // ~11 bytes

// ❌ Bad: Verbose keys
"application:module:product:entity:id:123"  // ~42 bytes (3.8x larger)
```

### Serialization Overhead

```csharp
// ✅ Good: Cache simple objects or DTOs
public record ProductDto(Guid Id, string Name, decimal Price);

// ⚠️ Caution: Large objects with navigation properties
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Category Category { get; set; }  // Included in serialization
    public List<Review> Reviews { get; set; }  // Could be large!
}

// ✅ Better: Project to DTO before caching
var productDto = await _db.Products
    .Where(p => p.Id == id)
    .Select(p => new ProductDto(p.Id, p.Name, p.Price))
    .FirstOrDefaultAsync(ct);

await _cache.SetAsync(cacheKey, productDto, ...);
```

### TTL Guidelines

| Data Type | Recommended TTL | Rationale |
|-----------|----------------|-----------|
| User profile | 10-15 minutes | Changes infrequently |
| Product catalog | 5-10 minutes | Updated regularly |
| Cart/session data | 30 minutes | Active user data |
| Static content | 1-24 hours | Rarely changes |
| Computed reports | 1 hour | Expensive to recalculate |
| Real-time data | 10-30 seconds | Needs to be fresh |

### Memory Limits

Monitor memory usage:

```csharp
// ✅ Set memory limits in configuration
"CacheOptions": {
    "MemoryCacheSizeLimitMb": 512,  // L1 cache limit
    "EnableL2Cache": true             // Use Redis for larger datasets
}
```

---

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

### Development vs Production

**Development** (`appsettings.Development.json`):
```json
{
  "CacheOptions": {
    "EnableL2Cache": false,  // Disable Redis for local dev
    "DefaultAbsoluteExpirationSeconds": 60  // Shorter TTL for testing
  }
}
```

**Production** (`appsettings.json`):
```json
{
  "CacheOptions": {
    "EnableL2Cache": true,
    "RedisConnectionString": "${REDIS_CONNECTION_STRING}",  // From environment
    "MemoryCacheSizeLimitMb": 1024,  // More memory in production
    "DefaultAbsoluteExpirationSeconds": 600
  }
}
```

---

## Testing Caching Behavior

### Unit Testing with Mock Cache

```csharp
public class ProductServiceTests
{
    [Fact]
    public async Task GetById_CacheHit_ReturnsCachedProduct()
    {
        // Arrange
        var mockCache = new Mock<ICacheService>();
        var cachedProduct = new Product { Id = Guid.NewGuid(), Name = "Cached" };
        
        mockCache.Setup(c => c.GetAsync<Product>(It.IsAny<string>(), default))
            .ReturnsAsync(Result<Product>.Success(cachedProduct));

        var service = new ProductService(_db, mockCache.Object, _logger);

        // Act
        var result = await service.GetByIdAsync(cachedProduct.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Cached", result.Data.Name);
        mockCache.Verify(c => c.GetAsync<Product>(It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task GetById_CacheMiss_FetchesFromDatabase()
    {
        // Arrange
        var mockCache = new Mock<ICacheService>();
        mockCache.Setup(c => c.GetAsync<Product>(It.IsAny<string>(), default))
            .ReturnsAsync(Result<Product>.Failure("Cache miss", 404));

        var product = new Product { Id = Guid.NewGuid(), Name = "FromDB" };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var service = new ProductService(_db, mockCache.Object, _logger);

        // Act
        var result = await service.GetByIdAsync(product.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("FromDB", result.Data.Name);
        
        // Verify cache was set
        mockCache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<Product>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<TimeSpan?>(),
            default), Times.Once);
    }
}
```

### Integration Testing Cache Invalidation

```csharp
[Fact]
public async Task Update_InvalidatesCache()
{
    // Arrange
    var product = new Product { Id = Guid.NewGuid(), Name = "Original" };
    _db.Products.Add(product);
    await _db.SaveChangesAsync();

    var service = new ProductService(_db, _cache, _logger);

    // Cache the product
    await service.GetByIdAsync(product.Id);

    // Act - Update
    var updateRequest = new UpdateProductRequest { Name = "Updated" };
    await service.UpdateAsync(product.Id, updateRequest);

    // Assert - Fetch again should get updated value, not cached
    var result = await service.GetByIdAsync(product.Id);
    Assert.Equal("Updated", result.Data.Name);
}
```

---

## Related Documentation

- [Output Caching & Response Compression](../../docs/caching-strategy.md)
- [Result Pattern Guide](./result-pattern-guide.md)
- [VSA Migration Guide](../guides/vsa-migration-guide.md)
- [Testing Patterns Guide](./testing-patterns.md)

---

**Last Updated**: 2025-11-20  
**Version**: 1.0  
**Author**: XFramework Development Team
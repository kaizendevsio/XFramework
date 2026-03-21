# Fix Caching Patterns

You are reviewing and fixing caching usage in XFramework services.

## Context
Read `docs/standards/xframework-best-practices.md` section 9 (Caching).

## Arguments
$ARGUMENTS should specify the service or module to review caching in.

## Steps

1. **Read the service code** that uses ICacheService
2. **Check cache key format** — must be `{module}:{entity}:{identifier}`
3. **Check invalidation** — writes must invalidate both specific keys and list prefixes
4. **Check TTL** — should be 5-10 minutes default
5. **Check graceful degradation** — cache failures must not crash the operation

## Correct Caching Pattern

```csharp
// Read-through with cache
public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct)
{
    var cacheKey = $"inventario:product:{id}";

    // Try cache first
    var cached = await cache.GetAsync<Product>(cacheKey, ct);
    if (cached is not null)
        return Result<Product>.Success(cached);

    // Database fallback
    var entity = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    if (entity is null)
        return Result<Product>.NotFound($"Product {id} not found");

    // Populate cache (fire-and-forget is OK here)
    await cache.SetAsync(cacheKey, entity, TimeSpan.FromMinutes(10), ct);

    return Result<Product>.Success(entity);
}

// Invalidation on write
public async Task<Result<Product>> CreateAsync(CreateRequest request, CancellationToken ct)
{
    // ... create entity ...

    // Invalidate list caches
    await cache.RemoveByPrefixAsync("inventario:product:list:", ct);

    return Result<Product>.Success(entity, 201);
}

// Invalidation on update/delete
public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
{
    // ... delete entity ...

    // Invalidate specific + list
    await cache.RemoveAsync($"inventario:product:{id}", ct);
    await cache.RemoveByPrefixAsync("inventario:product:list:", ct);

    return Result.Success();
}
```

## Common Issues

1. **Missing tenant in cache key** — multi-tenant data must include tenant: `inventario:product:{tenantId}:{id}`
2. **No invalidation on writes** — stale cache served after updates
3. **Cache exceptions crashing operations** — wrap cache calls in try-catch or rely on HybridCacheService's graceful degradation
4. **Too-long TTL** — >30 minutes is risky for frequently changing data
5. **Caching mutable objects** — cache serialized copies, not references
6. **Missing list cache invalidation** — updating one entity but list cache still serves old data

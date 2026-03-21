# Create or Refactor a Service

You are writing or refactoring a service class in XFramework following the VSA best practices.

## Context
Read `docs/standards/xframework-best-practices.md` sections 5 (Service Layer), 6 (Result Pattern), 8 (Data Access), 9 (Caching), and 10 (Observability).

## Arguments
$ARGUMENTS should specify: module name and service purpose (e.g., "Wallets WalletService" or "IdentityServer AuthService").

## Steps

1. **Read the existing service** if it exists, and identify what needs to change
2. **Read the reference**: `src/Modules/XFramework.Inventario/Inventario.Api/Services/ProductService.cs`
3. **Write/refactor the service** following the patterns below

## Service Template

```csharp
namespace [Module].Api.Services;

public class [Entity]Service(
    AppDbContext db,
    ICacheService cache,
    ILogger<[Entity]Service> logger)
{
    private static readonly ActivitySource ActivitySource = new(nameof([Entity]Service));

    public async Task<Result<[Entity]>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity($"{nameof([Entity]Service)}.{nameof(GetByIdAsync)}");
        activity?.SetTag("entity.id", id.ToString());

        try
        {
            // 1. Try cache
            var cached = await cache.GetAsync<[Entity]>($"[module]:[entity]:{id}", ct);
            if (cached is not null)
            {
                logger.LogDebug("[Entity] {Id} found in cache", id);
                return Result<[Entity]>.Success(cached);
            }

            // 2. Query database (use projection when possible)
            var entity = await db.[Entities]
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, ct);

            if (entity is null)
                return Result<[Entity]>.NotFound($"[Entity] {id} not found");

            // 3. Populate cache
            await cache.SetAsync($"[module]:[entity]:{id}", entity, TimeSpan.FromMinutes(10), ct);

            logger.LogInformation("[Entity] {Id} retrieved", id);
            return Result<[Entity]>.Success(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get [Entity] {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result<[Entity]>.Failure("Failed to retrieve [entity]");
        }
    }
}
```

## Rules to Enforce
- **Primary constructors** for DI — no manual `private readonly` field assignment
- **Every public method returns `Result<T>` or `Result`** — never throw for expected failures
- **`CancellationToken ct`** on every async method, passed through to EF/HTTP calls
- **No HTTP awareness** — no HttpContext, StatusCodes, or TypedResults in services
- **Logging levels:**
  - Debug: cache hits, query details
  - Information: successful operations
  - Warning: expected failures (not found, insufficient funds)
  - Error: unexpected exceptions
- **Structured logging** — use `{PropertyName}` templates, never string interpolation
- **OpenTelemetry Activity** per public method with semantic tags
- **Cache strategy:** read-through with invalidation on writes, prefix-based list invalidation
- **Cache keys:** `{module}:{entity}:{identifier}` format, include tenant ID for tenant-specific data
- **Use `.Select()` projections** for read-only DTOs when you don't need the full entity
- **Use `AsNoTracking()`** explicitly on read queries for clarity
- **Use `ExecuteUpdateAsync`/`ExecuteDeleteAsync`** for bulk operations
- **Invalidate cache** on Create/Update/Delete — both specific key and list prefix
- **Try-catch only around I/O operations** — let truly unexpected exceptions propagate

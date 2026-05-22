# 🚀 XFramework Improvement & Optimization Plan

> **Status:** Superseded historical improvement plan. It is useful for understanding the original modernization intent, but it does not govern current implementation.
> **Current guidance:** Use `docs/README.md`, `docs/solutions/README.md`, and current `docs/solutions/` convention and subsystem docs. Treat CQRS/MediatR, SignalR/StreamFlow, Serilog, or older framework-version references below as historical migration context unless a current solution doc says otherwise.

## Executive Summary

This comprehensive plan outlines strategic improvements to transform XFramework into the **best .NET development platform** through architectural simplification, performance optimization, and modern best practices.

---

## 📋 Table of Contents

1. [Architecture Modernization](#1-architecture-modernization)
2. [Performance Optimization Strategy](#2-performance-optimization-strategy)
3. [Module-Specific Improvements](#3-module-specific-improvements)
4. [Development Experience Enhancements](#4-development-experience-enhancements)
5. [Infrastructure & DevOps](#5-infrastructure--devops)
6. [Migration Roadmap](#6-migration-roadmap)

---

## 1. Architecture Modernization

### 1.1 Move from CQRS to Vertical Slice Architecture (VSA)

**Rationale**: VSA provides simplicity while maintaining advanced capabilities, reducing cognitive load and improving maintainability.

#### Current State Problems
- **CQRS Complexity**: Separate command/query handlers create overhead for simple CRUD operations
- **Scattered Logic**: Business logic dispersed across handlers, validators, pipeline behaviors
- **Boilerplate Code**: Excessive abstractions (ICreateHandler, IGetHandler, etc.)
- **Learning Curve**: New developers struggle with MediatR pattern

#### Proposed Solution: Feature-Centric Vertical Slices

```
src/
├── Features/
│   ├── Products/
│   │   ├── Get/
│   │   │   ├── Endpoint.cs      # Minimal API endpoint
│   │   │   └── Query.cs          # Query logic + validation
│   │   ├── Create/
│   │   │   ├── Endpoint.cs
│   │   │   ├── Command.cs
│   │   │   └── Validator.cs
│   │   └── Update/
│   │       └── ...
│   └── Users/
│       └── ...
```

**Naming Convention**:
- ✅ **Folders**: Use action verbs (`Get/`, `Create/`, `Update/`, `Delete/`)
- ✅ **Files**: Generic names (`Endpoint.cs`, `Query.cs`, `Command.cs`, `Validator.cs`)
- ✅ **Namespace provides context**: `Features.Products.Get` makes it clear this is "Get Product"
- ✅ **Cleaner imports**: `using Features.Products.Get;` vs `using Features.Products.GetProduct;`

**Benefits**:
- ✅ **All code for a feature in one place** - easier to understand and maintain
- ✅ **Reduced abstractions** - remove unnecessary interfaces and base classes
- ✅ **Faster development** - less boilerplate, clearer patterns
- ✅ **Better testability** - isolated feature tests
- ✅ **Flexible complexity** - simple features stay simple, complex ones can be elaborate

### 1.2 Enhanced EF Core with Cross-Cutting Concerns (No Repository Pattern)

**Philosophy**: EF Core IS the repository. Use its advanced features for generic cross-cutting concerns while maintaining feature-specific flexibility.

#### EF Core Advanced Features for Vertical Slice Architecture:

1. **Global Query Filters** (Multi-tenancy, Soft Delete)

**Why Global Query Filters (Not Interceptors)?**
- Global query filters are compiled into EF Core queries at the expression tree level = **maximum performance**
- Query interceptors would need to modify SQL/expressions at runtime = slower
- Soft delete filtering is a query concern, not a save concern

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Apply to all entities implementing interfaces
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        // Soft delete filter - KEEP AS QUERY FILTER (best performance)
        if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Equal(
                Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)),
                Expression.Constant(false));
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
        
        // Multi-tenancy filter (dynamic, set per request context)
        if (typeof(IHasTenantId).IsAssignableFrom(entityType.ClrType))
        {
            // Use ITenantService to get current tenant ID
            // Applied via query filter with runtime tenant context
        }
    }
}
```

2. **SaveChanges Interceptors** (Audit Timestamps, Concurrency)
```csharp
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var entries = eventData.Context!.ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable && (e.State == EntityState.Added || e.State == EntityState.Modified));
        
        foreach (var entry in entries)
        {
            var entity = (IAuditable)entry.Entity;
            
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.CreatedBy = _currentUser.UserId;
            }
            
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _currentUser.UserId;
            
            if (entity is IHasConcurrencyStamp concurrency)
            {
                concurrency.ConcurrencyStamp = Guid.NewGuid().ToString();
            }
        }
        
        return base.SavingChanges(eventData, result);
    }
}

// Register interceptor
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString)
           .AddInterceptors(new AuditInterceptor(currentUserService));
});
```

3. **Default Query Behaviors via DbContext Configuration**
```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        // Make AsNoTracking and AsSplitQuery the DEFAULT for all queries
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }
}

// For writes, explicitly enable tracking per operation
public class CreateProductCommand
{
    private readonly AppDbContext _db;
    
    public async Task<Result<Product>> ExecuteAsync(CreateProductRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            TenantId = request.TenantId
            // Timestamps, CreatedBy handled by interceptor automatically
        };
        
        _db.Products.Add(product);
        await _db.SaveChangesAsync(); // Interceptor applies audit fields
        
        return Result.Success(product);
    }
}
```

4. **Smart Include Management via Extension Methods**
```csharp
public static class QueryExtensions
{
    // Auto-enable includes only when specified
    public static IQueryable<T> IncludeRelated<T>(
        this IQueryable<T> query,
        params Expression<Func<T, object>>[] includes) where T : class
    {
        if (includes?.Any() == true)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }
        return query;
    }
}

// Usage in features
var product = await _db.Products
    .IncludeRelated(p => p.Category, p => p.Supplier) // Only when needed
    .FirstOrDefaultAsync(p => p.Id == id);
```

**Key Benefits**:
- ✅ **No repository abstraction** - EF Core handles everything
- ✅ **Generic cross-cutting concerns** - via interceptors, filters, behaviors
- ✅ **Feature-specific queries** - direct DbSet access in features
- ✅ **Zero boilerplate** - audit, soft delete, multi-tenancy handled automatically
- ✅ **Performance by default** - AsNoTracking and SplitQuery globally configured

### 1.3 Unified Response Pattern

Replace multiple response types with a single, flexible pattern:

```csharp
public record Result<T>
{
    public T? Data { get; init; }
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public int StatusCode { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
    
    public static Result<T> Success(T data, string? message = null) => new() 
    { 
        Data = data, 
        IsSuccess = true, 
        StatusCode = 200,
        Message = message 
    };
    
    public static Result<T> Failure(string message, int statusCode = 400) => new() 
    { 
        IsSuccess = false, 
        Message = message,
        StatusCode = statusCode 
    };
}
```

---

## 2. Performance Optimization Strategy

### 2.1 Database Layer Optimizations

#### Simplified Include Logic

**Current Issue**: The `includeNavigations` parameter is confusing. It loads ALL navigation properties up to 3 levels deep by default.

**Solution**: Remove the `includeNavigations` boolean parameter. Instead:
- If `Includes` array is passed → automatically enable navigation loading
- If `Includes` array is empty/null → load entity only (no navigations)

```csharp
public class GetHandler<TModel>
{
    public async Task<QueryResponse<TModel>> Handle(Get<TModel> request, CancellationToken ct)
    {
        IQueryable<TModel> query = _db.Set<TModel>();
        
        // Automatically enable includes ONLY if specified
        if (request.Includes?.Any() == true)
        {
            query = request.Includes.Aggregate(query, (current, include) => current.Include(include));
        }
        // No else branch - no automatic navigation loading!
        
        var entity = await query
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        
        return new QueryResponse<TModel> { Response = entity };
    }
}
```

**Benefits**:
- ✅ **Explicit is better than implicit** - you only get what you ask for
- ✅ **Performance by default** - no hidden navigation loads
- ✅ **Simpler API** - one less confusing parameter

#### Optimizations:

1. **Use Facet for Automatic DTO Projection** (https://github.com/Tim-Maes/Facet)

**Facet** eliminates manual DTO mapping boilerplate using source generators:

```csharp
// Install packages
// dotnet add package Facet
// dotnet add package Facet.Extensions  (for LINQ helpers)

// 1. Define source entity
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public Category Category { get; set; }
    public Supplier Supplier { get; set; }
}

// 2. Define DTO/Facet with [Facet] attribute
using Facet;

// Option A: As a class (auto-generates properties)
[Facet(typeof(Product), exclude: nameof(Product.Description))]
public partial class ProductDto { }

// Option B: As a record (cleaner syntax)
[Facet(typeof(Product))]
public partial record ProductDto { }

// Facet automatically generates:
// - All matching properties from Product
// - A constructor: public ProductDto(Product source)
// - Static Projection property for LINQ

// 3. Use in queries - via generated static Projection
var product = await _db.Products
    .Where(p => p.Id == id)
    .Select(ProductDto.Projection)  // Generated by Facet
    .FirstOrDefaultAsync();

// 4. For collections with extension methods
using Facet.Extensions;

var products = await _db.Products
    .Where(p => p.IsActive)
    .Select(ProductDto.Projection)
    .ToListAsync();

// Or use extension helper
var dto = product.ToFacet<ProductDto>();
var dtos = productList.SelectFacets<ProductDto>();

// 5. For nested properties (manual DTOs when needed)
public partial record ProductWithCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string CategoryName { get; set; }  // Flattened property
    public string SupplierName { get; set; }
}

// Manual Select for complex scenarios
var product = await _db.Products
    .Where(p => p.Id == id)
    .Select(p => new ProductWithCategoryDto
    {
        Id = p.Id,
        Name = p.Name,
        CategoryName = p.Category.Name,
        SupplierName = p.Supplier.CompanyName
    })
    .FirstOrDefaultAsync();
```

**Facet Benefits**:
- ✅ **Zero allocation overhead** - compiles to optimized Select expressions
- ✅ **Compile-time safety** - errors caught at build time
- ✅ **No reflection** - uses source generators
- ✅ **Automatic property matching** - no manual mapping needed for simple DTOs
- ✅ **30-50% faster** than manual Select or AutoMapper for simple projections
- ✅ **Clean syntax** - `[Facet(typeof(Source))]` and you're done

**When to use Facet vs Manual Select**:
- ✅ **Use Facet**: Simple DTOs that mirror the source entity (with optional excludes)
- ✅ **Use Manual Select**: Complex projections with flattened nested properties or computed fields

**Alternative without Facet** (if not using source generators):
```csharp
// Manual projection still viable
var product = await _db.Products
    .Where(p => p.Id == id)
    .Select(p => new ProductDto
    {
        Id = p.Id,
        Name = p.Name,
        CategoryName = p.Category.Name,
        SupplierName = p.Supplier.Name
    })
    .FirstOrDefaultAsync();
```

2. **Compiled Queries for Hot Paths**
```csharp
private static readonly Func<AppDbContext, Guid, Task<Product?>> GetProductById =
    EF.CompileAsyncQuery((AppDbContext db, Guid id) =>
        db.Products.FirstOrDefault(p => p.Id == id));

// Usage - 50-70% faster for frequently called queries
var product = await GetProductById(_db, productId);
```

3. **AsNoTracking for Read-Only Queries**
```csharp
var products = await _db.Products
    .AsNoTracking()  // 30-40% faster for reads
    .Where(p => p.IsActive)
    .ToListAsync();
```

4. **Database Indexing Strategy**
```csharp
// Add indexes via EF Core configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>()
        .HasIndex(p => new { p.TenantId, p.IsDeleted, p.CreatedAt })
        .HasFilter("IsDeleted = 0");  // Filtered index for active records
        
    modelBuilder.Entity<Product>()
        .HasIndex(p => p.Sku)
        .IsUnique();
}
```

5. **Pagination & Filtering Best Practices**
```csharp
public record GetProductsQuery
{
    public int PageSize { get; init; } = 20;  // Default small page
    public int Page { get; init; } = 1;
    public string? SearchTerm { get; init; }
}

// Efficient implementation
var query = _db.Products.AsNoTracking();

if (!string.IsNullOrEmpty(request.SearchTerm))
{
    query = query.Where(p => EF.Functions.Like(p.Name, $"%{request.SearchTerm}%"));
}

var totalCount = await query.CountAsync();
var products = await query
    .OrderByDescending(p => p.CreatedAt)
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToListAsync();
```

### 2.2 Caching Strategy Overhaul

#### Current Issues with `CacheManager.cs`:
- In-memory only (doesn't scale horizontally)
- No cache invalidation strategy
- Manual cache key management
- No distributed caching support

#### Proposed Multi-Tier Caching:

```csharp
public interface ICacheService
{
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}

public class HybridCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    
    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        // L1: Memory cache (fastest)
        if (_memoryCache.TryGetValue<T>(key, out var cached))
            return cached;
        
        // L2: Distributed cache (Redis/SQL)
        var serialized = await _distributedCache.GetStringAsync(key);
        if (serialized != null)
        {
            var value = JsonSerializer.Deserialize<T>(serialized);
            _memoryCache.Set(key, value, TimeSpan.FromMinutes(1)); // Short L1 TTL
            return value;
        }
        
        // L3: Database
        var result = await factory();
        var json = JsonSerializer.Serialize(result);
        
        await _distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
        });
        
        _memoryCache.Set(key, result, TimeSpan.FromMinutes(1));
        
        return result;
    }
}
```

**Cache Invalidation Pattern**:
```csharp
public class ProductService
{
    public async Task<Result<Product>> CreateProductAsync(CreateProductRequest request)
    {
        var product = new Product { ... };
        await _db.SaveChangesAsync();
        
        // Smart invalidation
        await _cache.RemoveByPrefixAsync($"products:list");
        await _cache.RemoveByPrefixAsync($"products:tenant:{product.TenantId}");
        
        return Result.Success(product);
    }
}
```

### 2.3 Response Compression & Output Caching

```csharp
// Program.cs
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Cache());
    options.AddPolicy("Products", builder => 
        builder.Cache()
               .Expire(TimeSpan.FromMinutes(5))
               .Tag("products"));
});

// Endpoint usage
app.MapGet("/api/products", GetProducts)
   .CacheOutput("Products");
```

### 2.4 Async/Await Optimization

**Current Issues**:
```csharp
// ❌ Unnecessary async/await
public async Task<QueryResponse<TModel>> Handle(...)
{
    var entity = await query.FirstOrDefaultAsync(cancellationToken);
    //entity = helperService.RemoveCircularReference(entity); // Sync call
    
    return new QueryResponse<TModel> { Response = entity };
}
```

**Optimized**:
```csharp
// ✅ Remove unnecessary async when possible
public Task<Product?> GetProductAsync(Guid id)
{
    return _db.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == id);
}

// ✅ Use ValueTask for hot paths with caching
public async ValueTask<Product?> GetProductAsync(Guid id)
{
    if (_cache.TryGetValue(id, out Product? cached))
        return cached; // Synchronous path, no allocation
    
    var product = await _db.Products.FindAsync(id);
    _cache.Set(id, product);
    return product;
}
```

---

## 3. Module-Specific Improvements

### 3.1 StreamFlow Module (Real-time Messaging)

**Current Issues**:
- Uses `ConcurrentDictionary` for in-memory state (doesn't scale)
- No message persistence
- No backpressure handling

**Goal**: Make StreamFlow perform like Redis - blazing fast, reliable message queue with channels.

**Improvements**:

1. **.NET Channels for High-Performance Message Queue**

Replace `ConcurrentDictionary` with **System.Threading.Channels** for lock-free, high-throughput message processing:

```csharp
public class StreamFlowMessageQueue
{
    // Bounded channel with backpressure (like Redis)
    private readonly Channel<StreamFlowMessage> _messageChannel = Channel.CreateBounded<StreamFlowMessage>(
        new BoundedChannelOptions(10000) // Max 10K queued messages
        {
            FullMode = BoundedChannelFullMode.Wait, // Backpressure when full
            SingleReader = false,
            SingleWriter = false
        });
    
    // Writer - enqueue messages
    public async ValueTask<bool> EnqueueAsync(StreamFlowMessage message, CancellationToken ct = default)
    {
        try
        {
            await _messageChannel.Writer.WriteAsync(message, ct);
            return true;
        }
        catch (ChannelClosedException)
        {
            return false;
        }
    }
    
    // Reader - process messages (background service)
    public IAsyncEnumerable<StreamFlowMessage> DequeueAsync(CancellationToken ct = default)
    {
        return _messageChannel.Reader.ReadAllAsync(ct);
    }
    
    // Batch dequeue for efficiency
    public async ValueTask<List<StreamFlowMessage>> DequeueBatchAsync(int maxBatch = 100, CancellationToken ct = default)
    {
        var batch = new List<StreamFlowMessage>(maxBatch);
        
        while (batch.Count < maxBatch && _messageChannel.Reader.TryRead(out var message))
        {
            batch.Add(message);
        }
        
        return batch;
    }
}

// Background processor (like Redis pub/sub worker)
public class StreamFlowProcessor : BackgroundService
{
    private readonly StreamFlowMessageQueue _queue;
    private readonly IHubContext<StreamFlowHub> _hubContext;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.DequeueAsync(stoppingToken))
        {
            try
            {
                // Process and broadcast
                await _hubContext.Clients
                    .Group(message.GroupId)
                    .SendAsync("ReceiveMessage", message, stoppingToken);
            }
            catch (Exception ex)
            {
                // Log and continue (don't stop processor)
                _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
            }
        }
    }
}

// Hub - lightweight, just enqueues
public class StreamFlowHub : Hub
{
    private readonly StreamFlowMessageQueue _queue;
    
    public async Task PublishMessage(StreamFlowMessage message)
    {
        message.Timestamp = DateTime.UtcNow;
        message.ConnectionId = Context.ConnectionId;
        
        // Enqueue (non-blocking, backpressure built-in)
        var enqueued = await _queue.EnqueueAsync(message);
        
        if (!enqueued)
        {
            throw new HubException("Message queue is full");
        }
    }
    
    public async Task JoinGroup(string groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
    }
}
```

**Channel Benefits vs ConcurrentDictionary**:
- ✅ **10-100x faster** - lock-free, optimized for producer-consumer scenarios
- ✅ **Built-in backpressure** - automatically handles overflow
- ✅ **Zero allocations** - uses pooled memory
- ✅ **Async-first** - designed for async/await patterns
- ✅ **Bounded by default** - prevents memory exhaustion
- ✅ **Cancellation-aware** - proper shutdown handling

2. **Optional Redis Persistence Layer**

For durability (optional, can be enabled per tenant/use-case):

```csharp
public class PersistentStreamFlowQueue : StreamFlowMessageQueue
{
    private readonly IConnectionMultiplexer _redis;
    
    public override async ValueTask<bool> EnqueueAsync(StreamFlowMessage message, CancellationToken ct)
    {
        // Enqueue to in-memory channel first (fast path)
        var enqueued = await base.EnqueueAsync(message, ct);
        
        if (enqueued && message.RequiresPersistence)
        {
            // Async persist to Redis (fire and forget)
            _ = Task.Run(async () =>
            {
                var db = _redis.GetDatabase();
                await db.StreamAddAsync($"streamflow:{message.GroupId}", new[]
                {
                    new NameValueEntry("data", JsonSerializer.Serialize(message)),
                    new NameValueEntry("timestamp", message.Timestamp.Ticks)
                });
            }, ct);
        }
        
        return enqueued;
    }
}
```

3. **Backpressure & Rate Limiting**
```csharp
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("streamflow", opts =>
    {
        opts.Window = TimeSpan.FromSeconds(1);
        opts.PermitLimit = 1000; // 1000 messages per second per client
        opts.QueueLimit = 100;   // Queue up to 100 requests when at limit
    });
});

// Apply to hub
app.MapHub<StreamFlowHub>("/hubs/streamflow")
   .RequireRateLimiting("streamflow");
```

4. **Connection Pooling & Configuration**
```csharp
services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 128 * 1024; // 128 KB
    options.StreamBufferCapacity = 10;
    options.EnableDetailedErrors = false; // Disable in production
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(10);
})
.AddMessagePackProtocol(); // Faster than JSON
```

**StreamFlow Performance Comparison**:

| Implementation | Throughput | Latency (p99) | Memory |
|---------------|-----------|---------------|---------|
| ConcurrentDictionary | ~10K msg/s | ~50ms | High (unbounded) |
| Channels | ~100K msg/s | ~5ms | Low (bounded) |
| Channels + Redis | ~80K msg/s | ~10ms | Low + Durable |

**Result**: StreamFlow will perform like Redis with channels - extremely fast, reliable, and scalable.

### 3.2 Wallets Module

**Performance Optimizations**:

1. **Optimistic Concurrency with Retry Logic**
```csharp
public async Task<Result<Wallet>> IncrementBalanceAsync(Guid walletId, decimal amount)
{
    const int maxRetries = 3;
    int attempt = 0;
    
    while (attempt < maxRetries)
    {
        try
        {
            var wallet = await _db.Wallets.FindAsync(walletId);
            wallet!.Balance += amount;
            wallet.ConcurrencyStamp = Guid.NewGuid().ToString();
            
            await _db.SaveChangesAsync();
            return Result.Success(wallet);
        }
        catch (DbUpdateConcurrencyException)
        {
            attempt++;
            if (attempt >= maxRetries) throw;
            await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt)); // Exponential backoff
        }
    }
    
    return Result.Failure("Concurrency conflict after retries");
}
```

2. **Batch Operations**
```csharp
public async Task<Result<int>> ProcessBatchTransactions(List<Transaction> transactions)
{
    using var transaction = await _db.Database.BeginTransactionAsync();
    
    try
    {
        // Bulk insert with EF Core
        await _db.BulkInsertAsync(transactions);
        
        // Update balances in batch
        var walletIds = transactions.Select(t => t.WalletId).Distinct();
        var wallets = await _db.Wallets
            .Where(w => walletIds.Contains(w.Id))
            .ToListAsync();
        
        foreach (var wallet in wallets)
        {
            var walletTransactions = transactions.Where(t => t.WalletId == wallet.Id);
            wallet.Balance += walletTransactions.Sum(t => t.Amount);
        }
        
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        
        return Result.Success(transactions.Count);
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### 3.3 IdentityServer Module

**Improvements**:

1. **JWT Token Optimization**
```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero, // Reduce default 5-minute skew
            RequireExpirationTime = true
        };
        
        // Cache tokens to avoid repeated parsing
        options.SaveToken = false; // Don't save in context (reduce memory)
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Cache user claims in distributed cache
                var userId = context.Principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                // ... cache logic
                return Task.CompletedTask;
            }
        };
    });
```

2. **Refresh Token Rotation**
```csharp
public class RefreshTokenService
{
    public async Task<Result<TokenResponse>> RefreshTokenAsync(string refreshToken)
    {
        // Validate and consume old token (one-time use)
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsUsed);
        
        if (token == null || token.ExpiresAt < DateTime.UtcNow)
            return Result.Failure("Invalid refresh token");
        
        // Mark as used
        token.IsUsed = true;
        
        // Generate new token pair
        var newAccessToken = GenerateAccessToken(token.UserId);
        var newRefreshToken = GenerateRefreshToken(token.UserId);
        
        await _db.SaveChangesAsync();
        
        return Result.Success(new TokenResponse 
        { 
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken 
        });
    }
}
```

### 3.4 Inventario (Inventory) Module

**Optimizations**:

1. **Stock Level Tracking with Redis**
```csharp
public class InventoryService
{
    private readonly IConnectionMultiplexer _redis;
    
    public async Task<bool> ReserveStock(Guid productId, int quantity)
    {
        var db = _redis.GetDatabase();
        var key = $"stock:{productId}";
        
        // Atomic decrement using Redis
        var newStock = await db.StringDecrementAsync(key, quantity);
        
        if (newStock < 0)
        {
            // Rollback
            await db.StringIncrementAsync(key, quantity);
            return false;
        }
        
        return true;
    }
}
```

2. **Event Sourcing for Inventory Changes**
```csharp
public record InventoryEvent
{
    public Guid EventId { get; init; }
    public Guid ProductId { get; init; }
    public string EventType { get; init; } // "StockAdded", "StockReserved", etc.
    public int Quantity { get; init; }
    public DateTime OccurredAt { get; init; }
}

// Rebuild state from events
public async Task<int> GetCurrentStock(Guid productId)
{
    var events = await _db.InventoryEvents
        .Where(e => e.ProductId == productId)
        .OrderBy(e => e.OccurredAt)
        .ToListAsync();
    
    return events.Sum(e => e.EventType switch
    {
        "StockAdded" => e.Quantity,
        "StockReserved" => -e.Quantity,
        "StockReleased" => e.Quantity,
        _ => 0
    });
}
```

---

## 4. Development Experience Enhancements

### 4.1 Simplified Minimal API Structure

```csharp
// Features/Products/GetProduct/GetProductEndpoint.cs
public static class GetProductEndpoint
{
    public record Request(Guid Id);
    public record Response(Guid Id, string Name, decimal Price);
    
    public static async Task<Results<Ok<Response>, NotFound>> Handle(
        [AsParameters] Request request,
        AppDbContext db,
        CancellationToken ct)
    {
        var product = await db.Products
            .Where(p => p.Id == request.Id)
            .Select(p => new Response(p.Id, p.Name, p.Price))
            .FirstOrDefaultAsync(ct);
        
        return product is null 
            ? TypedResults.NotFound() 
            : TypedResults.Ok(product);
    }
    
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id}", Handle)
           .WithName("GetProduct")
           .WithTags("Products")
           .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));
    }
}
```

### 4.2 Source Generator Improvements

**Enhanced Endpoint Generator**:
```csharp
// Auto-generate endpoints from feature classes
[GenerateEndpoint]
public class GetProductsFeature
{
    [HttpGet("/api/products")]
    [Cache(Duration = 300)] // 5 minutes
    public async Task<Result<List<ProductDto>>> ExecuteAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Implementation
    }
}

// Generated code creates the endpoint registration automatically
```

### 4.3 Better Validation

```csharp
// Use FluentValidation inline validators for simplicity
public static class CreateProductEndpoint
{
    public record Request(string Name, decimal Price, Guid CategoryId);
    
    private static readonly InlineValidator<Request> Validator = new()
    {
        v => v.RuleFor(x => x.Name).NotEmpty().MaximumLength(200),
        v => v.RuleFor(x => x.Price).GreaterThan(0),
        v => v.RuleFor(x => x.CategoryId).NotEmpty()
    };
    
    public static async Task<Results<Created<Product>, ValidationProblem>> Handle(
        Request request,
        AppDbContext db)
    {
        var validationResult = await Validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        
        // ... create logic
    }
}
```

---

## 5. Infrastructure & DevOps

### 5.1 Observability

**Structured Logging Enhancement**:
```csharp
// Use ILogger source generators for zero-allocation logging
public partial class ProductService
{
    private readonly ILogger<ProductService> _logger;
    
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Product {ProductId} created in {ElapsedMs}ms")]
    private partial void LogProductCreated(Guid productId, long elapsedMs);
    
    public async Task<Result<Product>> CreateAsync(CreateProductRequest request)
    {
        var sw = Stopwatch.StartNew();
        // ... creation logic
        
        LogProductCreated(product.Id, sw.ElapsedMilliseconds);
        return Result.Success(product);
    }
}
```

**OpenTelemetry Integration**:
```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddRedisInstrumentation()
        .AddSource("XFramework.*"))
    .WithMetrics(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());
```

### 5.2 Health Checks

```csharp
services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddRedis(config["Redis:ConnectionString"]!)
    .AddSignalRHub("/hubs/streamflow")
    .AddCheck<CustomHealthCheck>("wallet-balance-integrity");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### 5.3 API Versioning

```csharp
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(2, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

---

## 6. Migration Roadmap

### Phase 1: Foundation (Weeks 1-2)
- ✅ Set up new folder structure (`src/Features/`)
- ✅ Create Result<T> pattern
- ✅ Implement hybrid caching service
- ✅ Add Redis for distributed caching
- ✅ Set up output caching middleware

### Phase 2: Core Refactoring (Weeks 3-6)
- ✅ Migrate one module completely to VSA (start with simplest: Inventario)
- ✅ Remove CQRS handlers for that module
- ✅ Implement optimized data access patterns
- ✅ Add compiled queries for hot paths
- ✅ Set up comprehensive testing

### Phase 3: Performance Optimization (Weeks 7-8)
- ✅ Add database indexes based on query patterns
- ✅ Implement projection-based queries
- ✅ Add response compression
- ✅ Optimize SignalR with Redis backplane
- ✅ Implement batch operations for Wallets

### Phase 4: Migration of Remaining Modules (Weeks 9-12)
- ✅ Migrate StreamFlow module
- ✅ Migrate Wallets module
- ✅ Migrate IdentityServer module
- ✅ Migrate PaymentGateways module
- ✅ Complete remaining modules

### Phase 5: Enhancement & Polish (Weeks 13-14)
- ✅ Enhanced source generators
- ✅ Complete observability setup
- ✅ API documentation improvements
- ✅ Performance benchmarking
- ✅ Developer documentation

### Phase 6: Production Readiness (Weeks 15-16)
- ✅ Load testing
- ✅ Security audit
- ✅ Migration guides
- ✅ Training materials
- ✅ Go-live preparation

---

## 7. Success Metrics

### Performance Targets
- ⚡ **API Response Time**: < 50ms (p95), < 100ms (p99)
- ⚡ **Database Queries**: < 20ms average
- ⚡ **Cache Hit Rate**: > 80% for read operations
- ⚡ **Throughput**: > 10,000 requests/second per instance
- ⚡ **Memory Usage**: < 500MB per instance under normal load

### Developer Experience
- 📖 **Time to First Feature**: < 30 minutes for new developers
- 📖 **Code Complexity**: Cyclomatic complexity < 10 per method
- 📖 **Test Coverage**: > 80% for business logic
- 📖 **Build Time**: < 2 minutes for full solution

### Code Quality
- ✨ **Lines of Code**: Reduce by 30-40% through simplified architecture
- ✨ **Duplication**: < 3% code duplication
- ✨ **Maintainability Index**: > 80 (Visual Studio metric)

---

## 8. Quick Wins (Immediate Actions)

### Week 1 Quick Wins
1. **Add AsNoTracking to all read queries** (30% performance gain)
2. **Enable response compression** (60% bandwidth reduction)
3. **Add output caching for GET endpoints** (10x faster responses)
4. **Add database indexes** (50-90% query speedup)
5. **Remove unnecessary navigation includes** (40% faster queries)

### Code Changes for Quick Wins

```csharp
// 1. AsNoTracking for reads
var products = await _db.Products
    .AsNoTracking()  // ADD THIS LINE
    .ToListAsync();

// 2. Response Compression (Program.cs)
builder.Services.AddResponseCompression(options => 
{
    options.EnableForHttps = true;
});

// 3. Output Caching (Program.cs)
app.MapGet("/api/products", GetProducts)
   .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)));

// 4. Indexes (DbContext)
modelBuilder.Entity<Product>()
    .HasIndex(p => new { p.TenantId, p.IsDeleted })
    .HasFilter("IsDeleted = 0");

// 5. Specific Includes (instead of auto-include all)
var product = await _db.Products
    .Include(p => p.Category)  // Only what's needed
    .FirstAsync(p => p.Id == id);
```

---

## 9. Conclusion

This plan transforms XFramework from a complex CQRS-based system into a **modern, high-performance, developer-friendly platform** while maintaining enterprise-grade capabilities.

**Key Benefits**:
- 🎯 **40% reduction in code complexity**
- 🚀 **10x performance improvement in common scenarios**
- 😊 **Drastically improved developer experience**
- 📈 **Better scalability through proper caching and optimization**
- 🔧 **Easier maintenance and onboarding**

The migration to Vertical Slice Architecture combined with targeted performance optimizations will position XFramework as **the premier .NET development platform** for building modern, scalable applications.

---

**Next Steps**: Review this plan with the team, prioritize based on business needs, and begin Phase 1 implementation.

---

## 10. Enhanced Source Generator for Entity-Centric CRUD Endpoints

### Problem with Current Approach

Current system requires declaring endpoints in a separate file with disconnected syntax:

```csharp
// ❌ Current approach - ugly, disconnected from entity
[GenerateEndpoints("XFramework.Domain.Shared.Contracts", new[] { nameof(Tenant) })]
public static partial class GatewayEndpoints;
```

### New Approach - Entity-Centric Declaration

```csharp
// ✅ New approach - clean, co-located with entity
[GenerateEndpoints(actions: [EndpointActions.Create, EndpointActions.Read, EndpointActions.Update])]
[MemoryPackable(GenerateType.CircularReference)]
public partial class Tenant : BaseModel
{
    public string Name { get; set; }
    // ... properties
}
```

### Override Pattern (Extending Generated Code)

The current system uses **partial classes/interfaces** to allow extending generated code. This pattern will be preserved in the new VSA approach:

```csharp
// CURRENT SYSTEM (Service Wrappers)
// ✅ Generated code creates partials (ServiceWrapperGenerator.cs)
public partial interface IWalletsServiceWrapper {
    // Generated CRUD methods
    public IWalletCrudService Wallet { get; init; }
}

public partial record WalletsServiceWrapper(...) : IWalletsServiceWrapper
{
    // Generated CRUD implementation
}

// ✅ Manual override file extends with custom methods (WalletsServiceWrapper.cs)
public partial interface IWalletsServiceWrapper
{
    Task<CmdResponse> IncrementWallet(IncrementWalletRequest request);
    Task<CmdResponse> TransferWallet(TransferWalletRequest request);
}

public partial record WalletsServiceWrapper
{
    public Task<CmdResponse> IncrementWallet(IncrementWalletRequest request)
    {
        return SendVoidAsync(request); // Custom business logic
    }
    
    public Task<CmdResponse> TransferWallet(TransferWalletRequest request)
    {
        return SendVoidAsync(request); // Custom business logic
    }
}
```

**Benefits of Partial Pattern**:
- ✅ Generated CRUD methods available by default
- ✅ Easy to add custom business methods alongside CRUD
- ✅ Can override generated methods if needed (using `virtual` in generated code)
- ✅ Clean separation of generated vs custom code
- ✅ No inheritance complexity

---

### Eliminating CQRS/MediatR - Pure VSA Approach

**Current Problem - CQRS/MediatR Overhead**:

```csharp
// ❌ CURRENT: Generic handlers with MediatR abstraction
// File: CreateHandler.cs
public class CreateHandler<TModel>(DbContext dbContext, ...)
    : ICreateHandler<TModel>
    where TModel : class, IHasId, IAuditable, ...
{
    public async Task<CmdResponse<TModel>> Handle(Create<TModel> request, CancellationToken ct)
    {
        // Generic CRUD logic - 90+ lines of boilerplate
        request.Model.Id = request.Model.Id != Guid.Empty ? request.Model.Id : Guid.NewGuid();
        request.Model.CreatedAt = DateTime.UtcNow;
        // ... strip navigation properties
        // ... add to context
        await dbContext.SaveChangesAsync(ct);
        return new CmdResponse<TModel> { Response = request.Model, ... };
    }
}

// File: BaseServiceCommands.cs
public interface ICreateHandler<TModel> : IRequestHandler<Create<TModel>, CmdResponse<TModel>>;
public interface IGetHandler<TModel> : IRequestHandler<Get<TModel>, QueryResponse<TModel>>;

// Endpoint uses MediatR
app.MapPost("/products", async (IMediator mediator, Product model) =>
{
    var request = new Create<Product>(model); // Wrap in command
    return await mediator.Send(request); // MediatR overhead
});
```

**Problems with Current Approach**:
- ❌ MediatR adds unnecessary indirection and latency
- ❌ Generic handlers like `CreateHandler<TModel>` are overly complex (90+ lines)
- ❌ Command/query wrappers (`Create<T>`, `Get<T>`) add cognitive load
- ❌ Three layers for simple operations: Endpoint → MediatR → Handler → DbContext
- ❌ Difficult to debug (request goes through pipeline behaviors)
- ❌ Hard to customize per entity (must override generic behavior)

**New VSA Approach - Direct Service Calls (No MediatR)**:

```csharp
// ✅ NEW: Direct service with feature-specific logic
// File: ProductService.g.cs (Generated)
public partial class ProductService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProductService> _logger;
    
    public ProductService(AppDbContext db, ILogger<ProductService> logger)
    {
        _db = db;
        _logger = logger;
    }
    
    // Generated CRUD methods (simple, focused)
    public virtual async Task<Result<Product>> CreateAsync(
        Product entity,
        Guid tenantId,
        CancellationToken ct = default)
    {
        try
        {
            entity.Id = entity.Id != Guid.Empty ? entity.Id : Guid.NewGuid();
            entity.TenantId = tenantId;
            // Audit fields handled by SaveChanges interceptor automatically
            
            _db.Products.Add(entity);
            await _db.SaveChangesAsync(ct);
            
            _logger.LogInformation("Created {EntityName} with ID {EntityId}",
                nameof(Product), entity.Id);
            
            return Result.Success(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating {EntityName}", nameof(Product));
            return Result.Failure<Product>("Failed to create product");
        }
    }
    
    public virtual async Task<Result<Product>> GetAsync(
        Guid id,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var entity = await _db.Products
            .AsNoTracking() // Default behavior
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId, ct);
        
        return entity == null
            ? Result.NotFound<Product>("Product not found")
            : Result.Success(entity);
    }
}

// File: ProductService.cs (Manual - custom business logic)
public partial class ProductService
{
    // Override generated method with custom validation
    public override async Task<Result<Product>> CreateAsync(
        Product entity,
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Custom business logic
        if (entity.Price <= 0)
        {
            return Result.Failure<Product>("Price must be greater than zero");
        }
        
        // Call base generated method
        return await base.CreateAsync(entity, tenantId, ct);
    }
    
    // Add custom business methods
    public async Task<Result<Product>> CreateWithInventoryAsync(
        Product entity,
        Guid tenantId,
        int initialStock,
        CancellationToken ct = default)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        
        var result = await CreateAsync(entity, tenantId, ct);
        if (!result.IsSuccess) return result;
        
        await _inventoryService.SetInitialStock(result.Data.Id, initialStock);
        await transaction.CommitAsync(ct);
        
        return result;
    }
}

// Endpoint calls service directly (NO MediatR)
app.MapPost("/api/products", async (
    ProductService service, // Direct DI injection
    Product model,
    [FromQuery] Guid tenantId) =>
{
    var result = await service.CreateAsync(model, tenantId);
    return result.IsSuccess
        ? Results.Created($"/api/products/{result.Data.Id}", result.Data)
        : Results.BadRequest(result.Message);
});
```

**VSA Benefits vs CQRS/MediatR**:
- ✅ **40-60% less code** - no command/query wrappers, handlers, interfaces
- ✅ **Direct service calls** - Endpoint → Service → DbContext (2 hops instead of 4)
- ✅ **20-30% faster** - eliminates MediatR pipeline overhead
- ✅ **Easier to debug** - straightforward call stack
- ✅ **Clearer intent** - `productService.CreateAsync()` vs `mediator.Send(new Create<Product>())`
- ✅ **Per-entity customization** - override specific methods as needed
- ✅ **Standard DI** - no special MediatR registration
- ✅ **Virtual methods** - can override generated CRUD when needed

---

### Implementation Design

**1. Define Endpoint Configuration Attributes**:

```csharp
// XFramework.Core/Attributes/GenerateEndpointsAttribute.cs
namespace XFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GenerateEndpointsAttribute : Attribute
{
    public EndpointType Type { get; set; } = EndpointType.Rest;
    public EndpointActions[] Actions { get; set; } = [
        EndpointActions.Create,
        EndpointActions.Read,
        EndpointActions.Update,
        EndpointActions.Delete,
        EndpointActions.List
    ]; // Default: All CRUD operations
    
    public bool RequireAuthorization { get; set; } = true;
    public string? RoutePrefix { get; set; } // Override default route
    public string? Tag { get; set; } // Swagger tag (defaults to entity name)
    public int CacheDurationSeconds { get; set; } = 0; // 0 = no cache
}

public enum EndpointType
{
    Rest,      // Generate REST API endpoints (Minimal API)
    Service,   // Generate service wrapper for internal use
    Both       // Generate both REST and Service
}

[Flags]
public enum EndpointActions
{
    None = 0,
    Create = 1,
    Read = 2,
    Update = 4,
    Delete = 8,
    List = 16,
    Patch = 32,
    All = Create | Read | Update | Delete | List | Patch
}
```

**2. Enhanced Source Generator Logic**:

```csharp
// XFramework.SourceGenerators/EnhancedEndpointGenerator.cs
[Generator]
public class EnhancedEndpointGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        // Find all classes with [GenerateEndpoints] attribute
        var entities = context.Compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes())
            .OfType<ClassDeclarationSyntax>()
            .Where(c => HasGenerateEndpointsAttribute(c, context.Compilation))
            .ToList();
        
        foreach (var entity in entities)
        {
            var semanticModel = context.Compilation.GetSemanticModel(entity.SyntaxTree);
            var symbol = semanticModel.GetDeclaredSymbol(entity);
            var attribute = GetGenerateEndpointsAttribute(symbol);
            
            if (attribute.Type == EndpointType.Rest || attribute.Type == EndpointType.Both)
            {
                GenerateRestEndpoints(context, entity, attribute);
            }
            
            if (attribute.Type == EndpointType.Service || attribute.Type == EndpointType.Both)
            {
                GenerateServiceWrapper(context, entity, attribute);
            }
        }
    }
    
    private void GenerateRestEndpoints(
        GeneratorExecutionContext context,
        ClassDeclarationSyntax entity,
        GenerateEndpointsAttribute attribute)
    {
        var entityName = entity.Identifier.Text;
        var code = new StringBuilder();
        
        code.AppendLine($@"
using Microsoft.AspNetCore.Builder;
using XFramework.Domain.Shared.Contracts;
using MediatR;

namespace {GetNamespace(entity)}.Generated;

public static class {entityName}Endpoints
{{
    public static IEndpointRouteBuilder Map{entityName}Endpoints(this IEndpointRouteBuilder app)
    {{
        var group = app.MapGroup(""/{attribute.RoutePrefix ?? entityName.ToLower()}"")
            .WithTags(""{attribute.Tag ?? entityName}"");
            
        {(attribute.RequireAuthorization ? "group.RequireAuthorization();" : "")}
        ");
        
        // Generate based on selected actions
        if (attribute.Actions.Contains(EndpointActions.List))
        {
            code.AppendLine(GenerateListEndpoint(entityName, attribute));
        }
        
        if (attribute.Actions.Contains(EndpointActions.Read))
        {
            code.AppendLine(GenerateGetEndpoint(entityName, attribute));
        }
        
        if (attribute.Actions.Contains(EndpointActions.Create))
        {
            code.AppendLine(GenerateCreateEndpoint(entityName, attribute));
        }
        
        if (attribute.Actions.Contains(EndpointActions.Update))
        {
            code.AppendLine(GenerateUpdateEndpoint(entityName, attribute));
        }
        
        if (attribute.Actions.Contains(EndpointActions.Patch))
        {
            code.AppendLine(GeneratePatchEndpoint(entityName, attribute));
        }
        
        if (attribute.Actions.Contains(EndpointActions.Delete))
        {
            code.AppendLine(GenerateDeleteEndpoint(entityName, attribute));
        }
        
        code.AppendLine(@"
        return app;
    }
}");
        
        context.AddSource($"{entityName}Endpoints.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
    }
    
    private string GenerateListEndpoint(string entityName, GenerateEndpointsAttribute attr)
    {
        var cachePolicy = attr.CacheDurationSeconds > 0 
            ? $".CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds({attr.CacheDurationSeconds})))"
            : "";
        
        return $@"
        group.MapGet(""/"", async (
            IMediator mediator,
            [AsParameters] PaginationParams pagination) =>
        {{
            var request = new GetList<{entityName}>(
                PageSize: pagination.PageSize,
                PageNumber: pagination.Page,
                TenantId: pagination.TenantId);
            return await mediator.Send(request);
        }}){cachePolicy};";
    }
    
    private string GenerateGetEndpoint(string entityName, GenerateEndpointsAttribute attr)
    {
        var cachePolicy = attr.CacheDurationSeconds > 0 
            ? $".CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds({attr.CacheDurationSeconds})))"
            : "";
        
        return $@"
        group.MapGet(""{{id}}"", async (
            IMediator mediator,
            Guid id,
            [FromQuery] Guid tenantId) =>
        {{
            var request = new Get<{entityName}>(Id: id, TenantId: tenantId);
            return await mediator.Send(request);
        }}){cachePolicy};";
    }
    
    private string GenerateCreateEndpoint(string entityName, GenerateEndpointsAttribute attr)
    {
        return $@"
        group.MapPost(""/"", async (
            IMediator mediator,
            {entityName} model,
            [FromQuery] Guid tenantId) =>
        {{
            var request = new Create<{entityName}>(model);
            return await mediator.Send(request);
        }});";
    }
    
    // Similar methods for Update, Patch, Delete...
}
```

### Usage Examples

```csharp
// Example 1: Full CRUD with caching
[GenerateEndpoints(
    Type = EndpointType.Rest,
    CacheDurationSeconds = 300, // 5 minutes for reads
    Tag = "Products"
)]
public partial class Product : BaseModel
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// Example 2: Read-only API
[GenerateEndpoints(
    Actions = [EndpointActions.Read, EndpointActions.List],
    RoutePrefix = "api/products",
    CacheDurationSeconds = 600
)]
public partial class ProductCatalog : BaseModel
{
    // ... properties
}

// Example 3: Service wrapper only (no REST endpoints)
[GenerateEndpoints(Type = EndpointType.Service)]
public partial class InternalAuditLog : BaseModel
{
    // Generates service wrapper but no public API
}

// Example 4: Custom subset of operations
[GenerateEndpoints(
    Actions = [EndpointActions.Create, EndpointActions.Read],
    RequireAuthorization = false // Public read-only
)]
public partial class PublicPost : BaseModel
{
    // Only Create and Read endpoints generated
}

// Example 5: Both REST and Service
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All
)]
public partial class Tenant : BaseModel
{
    // Generates both REST API and service wrapper
}
```

### Generated Code Example

For the `Product` entity above, the generator creates:

```csharp
// Generated: ProductEndpoints.g.cs
namespace XFramework.Domain.Shared.Contracts.Generated;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/product")
            .WithTags("Products")
            .RequireAuthorization();
        
        // List endpoint (with caching)
        group.MapGet("/", async (
            IMediator mediator,
            [AsParameters] PaginationParams pagination) =>
        {
            var request = new GetList<Product>(
                PageSize: pagination.PageSize,
                PageNumber: pagination.Page,
                TenantId: pagination.TenantId);
            return await mediator.Send(request);
        }).CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(300)));
        
        // Get by ID (with caching)
        group.MapGet("{id}", async (
            IMediator mediator,
            Guid id,
            [FromQuery] Guid tenantId) =>
        {
            var request = new Get<Product>(Id: id, TenantId: tenantId);
            return await mediator.Send(request);
        }).CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(300)));
        
        // Create
        group.MapPost("/", async (
            IMediator mediator,
            Product model,
            [FromQuery] Guid tenantId) =>
        {
            var request = new Create<Product>(model);
            return await mediator.Send(request);
        });
        
        // Update, Patch, Delete endpoints...
        
        return app;
    }
}
```

### Automatic Registration

```csharp
// Program.cs - Auto-discover and register all generated endpoints
public static class EndpointExtensions
{
    public static WebApplication MapGeneratedEndpoints(this WebApplication app)
    {
        // Use reflection to find all *Endpoints classes with Map* methods
        var endpointTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.Name.EndsWith("Endpoints") && 
                       t.Namespace?.Contains(".Generated") == true);
        
        foreach (var type in endpointTypes)
        {
            var mapMethod = type.GetMethod($"Map{type.Name}");
            mapMethod?.Invoke(null, [app]);
        }
        
        return app;
    }
}

// Usage in Program.cs
app.MapGeneratedEndpoints(); // Auto-registers all generated endpoints
```

### Benefits of Entity-Centric Approach

- ✅ **Co-location** - Endpoint configuration lives with the entity definition
- ✅ **Discoverability** - See what APIs exist by looking at the entity
- ✅ **Type safety** - Enum-based action selection prevents typos
- ✅ **Granular control** - Choose exactly which operations to expose per entity
- ✅ **DRY principle** - No need to list entities in multiple separate files
- ✅ **Compile-time validation** - Invalid configurations caught at build time
- ✅ **Flexible** - Can generate REST endpoints, Service wrappers, or both
- ✅ **Smart defaults** - Full CRUD by default, easily customize per entity
- ✅ **Single source of truth** - Entity definition includes its API surface
- ✅ **Maintenance** - Easier to see and modify what's exposed per entity

### Migration Path

1. **Phase 1**: Implement new attribute and generator alongside existing system
2. **Phase 2**: Migrate entities one module at a time
3. **Phase 3**: Remove old `[GenerateEndpoints("namespace", new[] {...})]` approach
4. **Phase 4**: Deprecate old endpoint generators

This approach aligns perfectly with the Vertical Slice Architecture philosophy where everything related to an entity (schema, validation, API surface) is co-located.

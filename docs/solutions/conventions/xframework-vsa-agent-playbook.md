---
title: "XFramework VSA Agent Playbook"
date: 2026-05-15
category: conventions
module: XFramework
problem_type: convention
component: assistant
severity: high
applies_when:
  - "Agents are migrating or reviewing XFramework code against the feature-centric VSA, Result pattern, direct service injection, EF Core, caching, testing, and cleanup rules"
tags: [ai-agents, vsa, migration, conventions, result-pattern, services]
---

# 🤖 AI Development Guide - XFramework VSA Migration

**Quick Reference for AI Agents working on XFramework refactoring**

---

## 🎯 Mission

Transform XFramework from **CQRS/MediatR** → **Vertical Slice Architecture (VSA)** with direct service calls.

**Timeline**: 16 weeks | **Phases**: 7 | **Status**: Planning

---

## 🚫 What to REMOVE

```csharp
// ❌ DELETE these patterns
- IRequestHandler<TRequest, TResponse>
- IMediator / mediator.Send()
- Create<T>, Get<T>, GetList<T> command/query wrappers
- CreateHandler<T>, GetHandler<T>, etc. generic handlers
- MediatR pipeline behaviors
- All MediatR registrations in DI
```

---

## ✅ What to CREATE

```csharp
// ✅ NEW: Direct service pattern
public partial class ProductService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProductService> _logger;
    
    // Virtual for override in manual partial
    public virtual async Task<Result<Product>> CreateAsync(
        Product entity, Guid tenantId, CancellationToken ct = default)
    {
        entity.Id = entity.Id != Guid.Empty ? entity.Id : Guid.NewGuid();
        entity.TenantId = tenantId;
        // Audit via SaveChanges interceptor
        
        _db.Products.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Result.Success(entity);
    }
}

// ✅ Endpoint: Direct injection (NO MediatR)
app.MapPost("/products", async (
    ProductService service, // Direct DI
    Product model,
    [FromQuery] Guid tenantId) =>
{
    var result = await service.CreateAsync(model, tenantId);
    return result.IsSuccess 
        ? Results.Created($"/products/{result.Data.Id}", result.Data)
        : Results.BadRequest(result.Message);
});
```

---

## 📋 Core Patterns

### 1. Result Pattern
```csharp
public record Result<T>
{
    public T? Data { get; init; }
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    
    public static Result<T> Success(T data) => new() { Data = data, IsSuccess = true };
    public static Result<T> Failure(string msg) => new() { IsSuccess = false, Message = msg };
    public static Result<T> NotFound(string msg) => new() { IsSuccess = false, Message = msg };
}
```

### 2. Service Layer (Generated + Manual)
```csharp
// File: ProductService.g.cs (GENERATED)
public partial class ProductService
{
    public virtual async Task<Result<Product>> CreateAsync(...) { }
    public virtual async Task<Result<Product>> GetAsync(...) { }
}

// File: ProductService.cs (MANUAL - extends generated)
public partial class ProductService
{
    // Override with custom logic
    public override async Task<Result<Product>> CreateAsync(...)
    {
        // Custom validation
        if (entity.Price <= 0) return Result.Failure("Invalid price");
        
        // Call base
        return await base.CreateAsync(entity, tenantId, ct);
    }
    
    // Add custom methods
    public async Task<Result<Product>> CreateWithInventoryAsync(...) { }
}
```

### 3. EF Core Defaults
```csharp
// DbContext configuration
public AppDbContext(DbContextOptions options) : base(options)
{
    ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking; // Default
}

protected override void OnConfiguring(DbContextOptionsBuilder builder)
{
    builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery); // Default
}

// Query patterns
var product = await _db.Products
    .AsNoTracking() // Explicit for clarity
    .FirstOrDefaultAsync(p => p.Id == id, ct);
```

### 4. Audit Interceptor
```csharp
// Handles CreatedAt, UpdatedAt, CreatedBy, UpdatedBy automatically
public class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(...)
    {
        foreach (var entry in eventData.Context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.CreatedBy = _currentUser.UserId;
            }
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SavingChanges(eventData, result);
    }
}
```

---

## 🔑 Critical Rules

1. **No MediatR** - Direct service injection in endpoints
2. **Virtual Methods** - All generated CRUD methods are `virtual` for override
3. **Partial Classes** - Use partials to extend generated code
4. **Result Pattern** - All service methods return `Result<T>`
5. **AsNoTracking** - Default for all reads, explicit tracking for writes
6. **Interceptors** - Audit fields handled automatically, don't set manually
7. **Soft Delete** - Use `IsDeleted = true`, handled by global query filter
8. **Tenant Isolation** - Always validate `tenantId`, handled by query filter

---

## 📁 File Organization

```
src/
├── Features/                    # NEW: VSA features
│   └── Products/
│       ├── Create/
│       │   ├── Endpoint.cs
│       │   └── Validator.cs
│       ├── Get/
│       └── Update/
├── Services/                    # Generated services
│   ├── Generated/
│   │   └── ProductService.g.cs  # Generated partial
│   └── ProductService.cs        # Manual partial (custom logic)
└── Endpoints/                   # Generated endpoints
    └── Generated/
        └── ProductEndpoints.g.cs
```

---

## 🔧 Common Tasks

### Task: Migrate a Module to VSA

1. **Delete CQRS**
   ```bash
   # Remove files
   rm CreateHandler.cs GetHandler.cs PatchHandler.cs DeleteHandler.cs
   rm Create.cs Get.cs GetList.cs Patch.cs Replace.cs Delete.cs
   ```

2. **Create Service**
   ```csharp
   // ProductService.cs
   public partial class ProductService
   {
       private readonly AppDbContext _db;
       
       public virtual async Task<Result<Product>> CreateAsync(...) { }
       public virtual async Task<Result<Product>> GetAsync(...) { }
   }
   ```

3. **Update Endpoints**
   ```csharp
   // Remove IMediator, inject service directly
   app.MapPost("/products", async (ProductService service, ...) => { });
   ```

4. **Remove MediatR DI**
   ```csharp
   // Delete: services.AddMediatR(...)
   // Add: services.AddScoped<ProductService>();
   ```

### Task: Add Custom Business Logic

```csharp
// Extend service with partial
public partial class ProductService
{
    public async Task<Result<Product>> CreateWithInventoryAsync(...)
    {
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        
        var result = await CreateAsync(entity, tenantId, ct);
        if (!result.IsSuccess) return result;
        
        await _inventoryService.SetInitialStock(result.Data.Id, initialStock);
        await tx.CommitAsync(ct);
        
        return result;
    }
}
```

### Task: Optimize Query Performance

```csharp
// 1. Add indexes
modelBuilder.Entity<Product>()
    .HasIndex(p => new { p.TenantId, p.IsDeleted })
    .HasFilter("IsDeleted = 0");

// 2. Use projections (Facet library)
[Facet(typeof(Product))]
public partial record ProductDto { }

var products = await _db.Products
    .Select(ProductDto.Projection) // Generated by Facet
    .ToListAsync(ct);

// 3. Compiled queries for hot paths
private static readonly Func<AppDbContext, Guid, Task<Product?>> GetById =
    EF.CompileAsyncQuery((AppDbContext db, Guid id) =>
        db.Products.FirstOrDefault(p => p.Id == id));
```

---

## 🎨 StreamFlow Channel Pattern

```csharp
// Replace ConcurrentDictionary with Channels
private readonly Channel<Message> _channel = Channel.CreateBounded<Message>(
    new BoundedChannelOptions(10000)
    {
        FullMode = BoundedChannelFullMode.Wait // Backpressure
    });

// Enqueue
await _channel.Writer.WriteAsync(message, ct);

// Dequeue (background service)
await foreach (var msg in _channel.Reader.ReadAllAsync(ct))
{
    await ProcessAsync(msg);
}
```

---

## 📊 Phase Checklist

**Current Phase**: ___ of 7

- [ ] Phase 1: Foundation (Weeks 1-2)
- [ ] Phase 2: Core Refactoring - Inventario (Weeks 3-6)
- [ ] Phase 3: Performance (Weeks 7-8)
- [ ] Phase 4: Module Migration (Weeks 9-12)
- [ ] Phase 5: Source Generators (Weeks 11-12)
- [ ] Phase 6: Observability (Weeks 13-14)
- [ ] Phase 7: Production (Weeks 15-16)

---

## 📚 Reference Files

- **Detailed Plan**: `XFramework-Improvement-Plan.md`
- **Full Roadmap**: `XFramework-Development-Roadmap.md`
- **Current State**: See `src/Kernel/XFramework.Core/DataAccess/`

---

## ⚠️ Before Starting ANY Task

1. Check current phase in `XFramework-Development-Roadmap.md`
2. Read the specific phase section
3. Verify no blockers exist for the task
4. Read related files to understand context
5. Plan the approach before coding

---

## ✅ After Completing ANY Task

1. **Build & Verify**
   ```bash
   dotnet build
   # Fix any compilation errors
   ```

2. **Run Tests**
   ```bash
   dotnet test
   # All tests must pass before marking complete
   ```

3. **Update Roadmap**
   - Check off the completed task in `XFramework-Development-Roadmap.md`
   - Add notes about decisions made or challenges encountered
   - Update status if phase is complete

4. **Update Documentation**
   - If pattern changed: Update this guide
   - If API changed: Update API docs
   - If behavior changed: Update user docs

5. **Code Quality Check**
   - Remove unused imports
   - Remove commented code
   - Ensure proper error handling exists
   - Verify logging is in place

6. **Commit & Document**
   ```bash
   git add .
   git commit -m "Phase X.Y: [Brief description]
   
   - Detailed change 1
   - Detailed change 2
   
   Closes #issue-number"
   ```

7. **Performance Verification** (if applicable)
   - Run benchmarks if performance-critical
   - Verify memory usage hasn't increased significantly
   - Check database query times

8. **Security Check** (if applicable)
   - Verify tenant isolation still works
   - Check authorization rules
   - Validate input sanitization

**⚠️ Do NOT mark a task complete until ALL steps above are done!**

---

## 🚀 Quick Start Commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run specific project
dotnet run --project src/Presentation/Gateway

# Generate code (after changes to source generators)
dotnet build /t:Rebuild
```

---

## 💡 Pro Tips

- **Start Small**: Migrate Inventario module first (simplest)
- **Test Often**: Each service method needs tests
- **Use Partials**: Never modify generated `.g.cs` files
- **Log Everything**: Use ILogger, not Console.WriteLine
- **Think VSA**: Feature folders group related code together
- **No Shortcuts**: Complete refactor, not hybrid CQRS/VSA

---

**Last Updated**: 2025-01-19 | **Version**: 1.0

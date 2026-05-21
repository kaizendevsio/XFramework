---
title: "XFramework VSA Agent Playbook"
date: 2026-05-21
category: conventions
module: XFramework
problem_type: convention
component: assistant
severity: high
applies_when:
  - "Agents are migrating or reviewing XFramework code against the feature-centric VSA, Result pattern, direct service injection, EF Core, caching, testing, and cleanup rules"
tags: [ai-agents, vsa, migration, conventions, result-pattern, services]
---

# AI Development Guide - XFramework VSA

**Quick Reference for AI Agents working on current XFramework features**

---

## Current Mission

Build and review XFramework features using **Vertical Slice Architecture (VSA)**, generated Minimal API registration, direct service calls, FluentValidation, and Bolt for internal RPC/streaming.

**Status**: Current implementation guidance. Historical CQRS/MediatR migration notes are only useful when removing old code.

---

## Do Not Use For New Work

```csharp
// Delete these only when migrating historical code.
- IRequestHandler<TRequest, TResponse>
- IMediator / mediator.Send()
- Create<T>, Get<T>, GetList<T> command/query wrappers
- CreateHandler<T>, GetHandler<T>, etc. generic handlers
- MediatR pipeline behaviors
- All MediatR registrations in DI
- StreamFlow or SignalR handlers for module RPC
```

---

## What to Create

```csharp
// Manual VSA endpoint: static handler with generated REST/Bolt registration.
public static class CreateProductEndpoint
{
    [MapPost("/api/products", Tags = ["Products"], Summary = "Create product")]
    [BoltHandler]
    public static async Task<Result<ProductResponse>> Handle(
        CreateProductRequest request,
        IValidator<CreateProductRequest> validator,
        ProductService service,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<ProductResponse>.Failure(validation.ToString());

        return await service.CreateAsync(request, ct);
    }
}

// Request contract used by BoltHandlerGenerator to infer the Bolt response.
public sealed record CreateProductRequest(string Name, decimal Price) :
    IBoltRequest<CreateProductRequest, Result<ProductResponse>>;
```

```csharp
// Program.cs: map generated endpoint routes once per API module.
app.MapGeneratedEndpoints();

// Manual endpoints are still explicit when generated binding is not enough.
GetWalletEndpoint.Map(app);
```

```csharp
// Entity-generated CRUD: opt in from the entity.
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/products")]
public partial class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
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

### 2. Service Layer (Manual Or Generated)
```csharp
public sealed class ProductService(AppDbContext db, ILogger<ProductService> logger)
{
    public async Task<Result<ProductResponse>> CreateAsync(
        CreateProductRequest request,
        CancellationToken ct)
    {
        var product = new Product { Name = request.Name, Price = request.Price };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        return Result<ProductResponse>.Success(ProductResponse.From(product));
    }
}
```

### 3. EF Core Defaults

Detailed data-access authority: `docs/solutions/conventions/ef-core-data-access-patterns.md`. This playbook only summarizes the day-to-day rules.

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

1. **No MediatR for new work** - Direct service injection in endpoints
2. **Use `[MapPost]`, `[MapGet]`, `[MapPut]`, `[MapPatch]`, or `[MapDelete]`** on generated manual endpoint handlers
3. **Use `[BoltHandler]` plus `IBoltRequest<TRequest, TResponse>`** when the same handler is callable over Bolt
4. **Map generated routes with `app.MapGeneratedEndpoints()`** in module `Program.cs`
5. **Result Pattern** - All service methods return `Result<T>` or `Result`
6. **AsNoTracking** - Default for reads, explicit tracking for writes
7. **Interceptors** - Audit fields handled automatically, don't set manually
8. **Soft Delete** - Use `IsDeleted = true`, handled by global query filter
9. **Tenant Isolation** - Always validate tenant behavior, handled by query filter where available

---

## 📁 File Organization

```
src/Modules/XFramework.[Module]/[Module].Api/
|-- Features/
|   `-- Products/
|       |-- Create/
|       |   |-- Endpoint.cs
|       |   `-- CreateProductValidator.cs
|       |-- Get/
|       `-- Update/
|-- Services/
|   `-- ProductService.cs
|-- Generated/
|   `-- GeneratedEndpointRoutes.g.cs
`-- Program.cs
```

---

## 🔧 Common Tasks

### Task: Add A Manual VSA Endpoint

1. **Create or reuse the service**
   ```csharp
   public sealed class ProductService(AppDbContext db)
   {
       public Task<Result<ProductResponse>> CreateAsync(CreateProductRequest request, CancellationToken ct) { ... }
   }
   ```

2. **Add an endpoint handler**
   ```csharp
   [MapPost("/api/products", Tags = ["Products"])]
   [BoltHandler]
   public static Task<Result<ProductResponse>> Handle(
       CreateProductRequest request,
       ProductService service,
       CancellationToken ct) => service.CreateAsync(request, ct);
   ```

3. **Ensure registration exists once**
   ```csharp
   builder.Services.AddScoped<ProductService>();
   builder.Services.AddValidatorsFromAssemblyContaining<Program>();
   app.MapGeneratedEndpoints();
   ```

### Task: Remove Historical CQRS/MediatR Code

Use this only when a module still contains old handlers.

1. Delete `IRequestHandler`, `IMediator`, and generic command/query wrappers.
2. Replace `mediator.Send(...)` with direct service calls from the endpoint.
3. Remove `services.AddMediatR(...)` registrations.
4. Keep manual endpoint mappings only where generator attributes cannot represent the binding.

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

## Bolt Handler Pattern

```csharp
// Domain.Shared request contract.
public sealed record TransferWalletRequest(Guid FromWalletId, Guid ToWalletId, decimal Amount) :
    IBoltRequest<TransferWalletRequest, Result<TransferWalletResponse>>;

// API feature handler. BoltHandlerGenerator emits an IBoltHandler and a REST adapter.
public static class TransferWalletEndpoint
{
    [MapPost("/api/wallets/transfer", Tags = ["Wallets"])]
    [BoltHandler]
    public static Task<Result<TransferWalletResponse>> Handle(
        TransferWalletRequest request,
        IWalletService walletService,
        CancellationToken ct) => walletService.TransferAsync(request, ct);
}
```

---

## 📚 Reference Files

- **Best Practices**: `docs/solutions/conventions/xframework-best-practices.md`
- **Generated Endpoint Registration**: `docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md`
- **GenerateEndpoints Attribute**: `docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md`
- **EF Core Data Access**: `docs/solutions/conventions/ef-core-data-access-patterns.md`
- **Caching Strategy**: `docs/solutions/best-practices/xframework-caching-strategy.md`
- **Feature Map**: `docs/solutions/conventions/xframework-feature-surface-map.md`

---

## ⚠️ Before Starting ANY Task

1. Read the existing module feature folder under `src/Modules/XFramework.[Module]/[Module].Api/Features/`.
2. Check whether the feature should be manual VSA, entity-generated CRUD, or both.
3. Inspect the module `Program.cs` for `app.MapGeneratedEndpoints()` and any explicit manual endpoint mappings.
4. Check request contracts under `[Module].Domain.Shared/Contracts/Requests/` before adding Bolt requests.
5. Read related tests to understand expected behavior.

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

3. **Update Documentation**
   - If pattern changed: Update this guide
   - If API changed: Update API docs
   - If behavior changed: Update user docs

4. **Code Quality Check**
   - Remove unused imports
   - Remove commented code
   - Ensure proper error handling exists
   - Verify logging is in place

5. **Performance Verification** (if applicable)
   - Run benchmarks if performance-critical
   - Verify memory usage hasn't increased significantly
   - Check database query times

6. **Security Check** (if applicable)
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

- **Start From Existing Modules**: IdentityServer, Wallets, Messaging, Community, and Inventario show current patterns
- **Test Often**: Each service method needs tests
- **Use Partials**: Never modify generated `.g.cs` files
- **Log Everything**: Use ILogger, not Console.WriteLine
- **Think VSA**: Feature folders group related code together
- **No New CQRS/MediatR**: If old patterns remain, treat them as migration targets

---

**Last Updated**: 2026-05-21 | **Version**: 2.0

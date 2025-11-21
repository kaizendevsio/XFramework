# Developer Onboarding Guide - XFramework

## Welcome to XFramework! 🚀

This guide will help you get started developing with XFramework's VSA (Vertical Slice Architecture). By the end of this guide, you'll understand the core concepts, have your environment set up, and be ready to implement your first feature.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Project Structure Overview](#project-structure-overview)
3. [Core Concepts Quick Reference](#core-concepts-quick-reference)
4. [Setup Instructions](#setup-instructions)
5. [Your First Feature Implementation](#your-first-feature-implementation)
6. [Development Workflow](#development-workflow)
7. [Troubleshooting Common Issues](#troubleshooting-common-issues)
8. [Next Steps](#next-steps)

---

## Prerequisites

### Required Software

| Tool | Version | Purpose |
|------|---------|---------|
| **.NET SDK** | 9.0+ | Runtime and compiler |
| **PostgreSQL** | 12+ | Database (or SQL Server) |
| **Redis** | 6.0+ | Distributed cache (optional for local dev) |
| **Git** | Latest | Version control |

### Recommended IDE

- **JetBrains Rider** (preferred) or **Visual Studio 2022** or **VS Code** with C# extension

### Optional Tools

- **Docker Desktop** - For running PostgreSQL and Redis locally
- **Postman** or **Insomnia** - API testing
- **Azure Data Studio** - Database management

---

## Project Structure Overview

### High-Level Organization

```
XFramework/
├── src/
│   ├── Kernel/                    # Core framework code
│   │   ├── XFramework.Core/       # Patterns, Result<T>, caching, extensions
│   │   └── XFramework.Domain/     # Base models, interfaces, DbContext
│   ├── Modules/                   # Business modules (features)
│   │   ├── XFramework.Wallets/    # Example: Wallets module
│   │   ├── XFramework.Identity/   # User management
│   │   └── [YourModule]/          # Your new modules here
│   ├── Presentation/              # API gateways and UI
│   │   └── Gateway/               # Main API gateway
│   └── SourceGenerators/          # Code generators
├── docs/                          # Project documentation
├── .ruru/docs/                    # Developer guides (you are here!)
└── tests/                         # Test projects
```

### Module Structure (Example: Wallets)

```
XFramework.Wallets/
├── Wallets.Api/                   # API endpoints (Minimal APIs)
├── Wallets.Core/                  # Business logic (Services)
│   └── Services/
│       ├── WalletService.cs       # Manual service code
│       └── Generated/
│           └── WalletService.g.cs # Generated CRUD (if applicable)
├── Wallets.Domain/                # Domain entities
│   └── Entities/
│       ├── Wallet.cs
│       └── WalletTransaction.cs
└── Wallets.Domain.Shared/         # DTOs and contracts
    └── Contracts/
        └── Requests/
            ├── IncrementWalletRequest.cs
            └── TransferWalletRequest.cs
```

---

## Core Concepts Quick Reference

### 1. Result<T> Pattern

**All service methods return `Result<T>`** instead of throwing exceptions:

```csharp
// ✅ Good: Returns Result<T>
public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    var product = await _db.Products.FindAsync(id);
    if (product == null)
        return Result<Product>.NotFound($"Product {id} not found");
    
    return Result<Product>.Success(product);
}

// ❌ Bad: Throws exceptions
public async Task<Product> GetByIdAsync(Guid id)
{
    var product = await _db.Products.FindAsync(id);
    if (product == null)
        throw new NotFoundException("Product not found"); // Don't do this!
    
    return product;
}
```

**Key Factory Methods:**
- `Result<T>.Success(data)` - Operation succeeded
- `Result<T>.Failure(message, statusCode)` - Operation failed
- `Result<T>.NotFound(message)` - Resource not found (404)
- `Result<T>.ValidationError(errors)` - Validation failed (400)

### 2. Direct Service Injection (No MediatR)

**Inject services directly** into endpoints:

```csharp
// ✅ VSA Pattern: Direct service injection
app.MapPost("/api/wallets/increment", async (
    IWalletService walletService,  // Inject service directly
    IncrementWalletRequest request) =>
{
    var result = await walletService.IncrementBalanceAsync(request);
    return result.IsSuccess 
        ? Results.Ok(result) 
        : Results.BadRequest(result.Message);
});

// ❌ Old CQRS: Via MediatR (deprecated)
app.MapPost("/api/wallets/increment", async (
    IMediator mediator,  // Don't use MediatR anymore
    IncrementWalletRequest request) =>
{
    return await mediator.Send(request);
});
```

### 3. Structured Logging

**Use `LoggerMessage` source generators** for high-performance logging:

```csharp
// ✅ Good: Use extension methods from LogMessages.cs
_logger.EntityCreated("Product", productId);
_logger.WalletIncremented(walletId, amount, "USD", newBalance);
_logger.ValidationFailed("CreateProduct", "Price must be positive");

// ❌ Bad: String interpolation (allocates memory)
_logger.LogInformation($"Created product {productId}");
```

### 4. Caching Strategy

**Use HybridCacheService** for application caching:

```csharp
public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    var cacheKey = $"product:{id}";
    
    // GetOrSet pattern - simplest approach
    return await _cacheService.GetOrSetAsync(
        cacheKey,
        async ct => await FetchFromDatabaseAsync(id, ct),
        absoluteExpiration: TimeSpan.FromMinutes(10),
        cancellationToken: ct);
}
```

### 5. Entity Framework Patterns

**Key practices:**
- ✅ Use `AsNoTracking()` for read operations
- ✅ Let `AuditInterceptor` handle audit fields automatically
- ✅ Use soft deletes (`IsDeleted = true`)
- ✅ Filter by `TenantId` for multi-tenancy

```csharp
// ✅ Read operation
var product = await _db.Products
    .AsNoTracking()  // No change tracking needed
    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

// ✅ Write operation (tracking enabled by default)
var product = await _db.Products.FindAsync(id);
product.Name = "Updated Name";
// CreatedAt, UpdatedAt, etc. handled by interceptor
await _db.SaveChangesAsync(ct);
```

---

## Setup Instructions

### Step 1: Clone Repository

```bash
git clone https://github.com/your-org/XFramework.git
cd XFramework
```

### Step 2: Configure Database

**Option A: Using Docker (Recommended for local dev)**

```bash
# Start PostgreSQL and Redis
docker-compose up -d

# This will start:
# - PostgreSQL on localhost:5432
# - Redis on localhost:6379
```

**Option B: Local PostgreSQL Installation**

1. Install PostgreSQL 12+
2. Create a database: `CREATE DATABASE xframework_dev;`
3. Update connection string in `appsettings.Development.json`

### Step 3: Update Configuration

Edit `src/Presentation/Gateway/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=xframework_dev;Username=postgres;Password=yourpassword"
  },
  "CacheOptions": {
    "EnableL2Cache": false,  // Disable Redis for local dev if not using Docker
    "EnableL1Cache": true
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

### Step 4: Run Database Migrations

```bash
cd src/Presentation/Gateway
dotnet ef database update
```

### Step 5: Build Solution

```bash
# From repository root
dotnet build
```

### Step 6: Run Application

```bash
cd src/Presentation/Gateway
dotnet run
```

### Step 7: Verify Installation

Open browser to `https://localhost:5001/swagger` - you should see the API documentation.

**Health Check:**
```bash
curl https://localhost:5001/health
```

Expected response:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567"
}
```

---

## Your First Feature Implementation

Let's implement a simple CRUD feature for "Categories" from scratch.

### Step 1: Create Domain Entity

Create `src/Modules/YourModule/YourModule.Domain/Entities/Category.cs`:

```csharp
using XFramework.Domain.Interfaces;

namespace YourModule.Domain.Entities;

public class Category : IEntity, IAuditable, ISoftDeletable, IHasTenantId
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // IAuditable
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    
    // ISoftDeletable
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // IHasTenantId
    public Guid TenantId { get; set; }
    
    // Additional properties
    public bool IsEnabled { get; set; } = true;
    public Guid ConcurrencyStamp { get; set; }
}
```

### Step 2: Add to DbContext

Add to your module's `DbContext` or the main `AppDbContext`:

```csharp
public DbSet<Category> Categories { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    modelBuilder.Entity<Category>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.HasIndex(e => e.Name);
        
        // Global query filter (soft delete + tenant isolation)
        entity.HasQueryFilter(e => !e.IsDeleted && e.TenantId == _currentTenantId);
    });
}
```

### Step 3: Create DTOs/Requests

Create `src/Modules/YourModule/YourModule.Domain.Shared/Contracts/Requests/`:

```csharp
// CreateCategoryRequest.cs
public record CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

// UpdateCategoryRequest.cs
public record UpdateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

// GetCategoryListRequest.cs
public record GetCategoryListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
```

### Step 4: Create Service Interface

Create `src/Modules/YourModule/YourModule.Core/Services/ICategoryService.cs`:

```csharp
using XFramework.Core.Patterns;

namespace YourModule.Core.Services;

public interface ICategoryService
{
    Task<Result<Category>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<Result<Category>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<List<Category>>> GetListAsync(GetCategoryListRequest request, CancellationToken ct = default);
    Task<Result<Category>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}
```

### Step 5: Implement Service

Create `src/Modules/YourModule/YourModule.Core/Services/CategoryService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Core.Loggers;
using XFramework.Core.Patterns;
using XFramework.Core.Services.Caching;

namespace YourModule.Core.Services;

public class CategoryService : ICategoryService
{
    private readonly DbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        DbContext db,
        ICacheService cache,
        ILogger<CategoryService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Category>> CreateAsync(
        CreateCategoryRequest request, 
        CancellationToken ct = default)
    {
        try
        {
            var category = new Category
            {
                Name = request.Name,
                Description = request.Description
            };

            _db.Set<Category>().Add(category);
            await _db.SaveChangesAsync(ct);

            // Invalidate list cache
            await _cache.RemoveByPrefixAsync("category:list:", ct);

            _logger.EntityCreated("Category", category.Id);
            return Result<Category>.Success(category, 201);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateCategory", "Category", Guid.Empty, ex.Message, ex);
            return Result<Category>.Failure("Failed to create category", 500);
        }
    }

    public async Task<Result<Category>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = $"category:{id}";

            return await _cache.GetOrSetAsync(
                cacheKey,
                async ct =>
                {
                    var category = await _db.Set<Category>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == id, ct);

                    if (category == null)
                        throw new KeyNotFoundException($"Category {id} not found");

                    return category;
                },
                absoluteExpiration: TimeSpan.FromMinutes(10),
                cancellationToken: ct);
        }
        catch (KeyNotFoundException)
        {
            return Result<Category>.NotFound($"Category {id} not found");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("GetCategory", "Category", id, ex.Message, ex);
            return Result<Category>.Failure("Failed to retrieve category", 500);
        }
    }

    public async Task<Result<List<Category>>> GetListAsync(
        GetCategoryListRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var categories = await _db.Set<Category>()
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(ct);

            return Result<List<Category>>.Success(categories);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("GetCategoryList", "Category", Guid.Empty, ex.Message, ex);
            return Result<List<Category>>.Failure("Failed to retrieve categories", 500);
        }
    }

    public async Task<Result<Category>> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var category = await _db.Set<Category>()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category == null)
                return Result<Category>.NotFound($"Category {id} not found");

            category.Name = request.Name;
            category.Description = request.Description;

            await _db.SaveChangesAsync(ct);

            // Invalidate cache
            await _cache.RemoveAsync($"category:{id}", ct);
            await _cache.RemoveByPrefixAsync("category:list:", ct);

            _logger.EntityUpdated("Category", id);
            return Result<Category>.Success(category);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("UpdateCategory", "Category", id, ex.Message, ex);
            return Result<Category>.Failure("Failed to update category", 500);
        }
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var category = await _db.Set<Category>()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category == null)
                return Result<bool>.NotFound($"Category {id} not found");

            category.IsDeleted = true;
            await _db.SaveChangesAsync(ct);

            // Invalidate cache
            await _cache.RemoveAsync($"category:{id}", ct);
            await _cache.RemoveByPrefixAsync("category:list:", ct);

            _logger.EntityDeleted("Category", id);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("DeleteCategory", "Category", id, ex.Message, ex);
            return Result<bool>.Failure("Failed to delete category", 500);
        }
    }
}
```

### Step 6: Register Service

Add to `src/Modules/YourModule/YourModule.Api/Installers/ServicesInstaller.cs`:

```csharp
public class ServicesInstaller : IInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        // ... other services
    }
}
```

### Step 7: Create Endpoints

Create `src/Modules/YourModule/YourModule.Api/Endpoints/CategoryEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace YourModule.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireAuthorization(); // Add if auth is needed

        // Create
        group.MapPost("/", async (
            ICategoryService service,
            [FromBody] CreateCategoryRequest request) =>
        {
            var result = await service.CreateAsync(request);
            return result.IsSuccess
                ? Results.Created($"/api/categories/{result.Data.Id}", result.Data)
                : Results.BadRequest(new { error = result.Message });
        })
        .WithName("CreateCategory")
        .Produces<Category>(201)
        .ProducesProblem(400);

        // Get by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            ICategoryService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.NotFound(new { error = result.Message });
        })
        .WithName("GetCategory")
        .Produces<Category>(200)
        .ProducesProblem(404);

        // Get list
        group.MapGet("/", async (
            [AsParameters] GetCategoryListRequest request,
            ICategoryService service) =>
        {
            var result = await service.GetListAsync(request);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.BadRequest(new { error = result.Message });
        })
        .WithName("GetCategories")
        .Produces<List<Category>>(200);

        // Update
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateCategoryRequest request,
            ICategoryService service) =>
        {
            var result = await service.UpdateAsync(id, request);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.NotFound(new { error = result.Message });
        })
        .WithName("UpdateCategory")
        .Produces<Category>(200)
        .ProducesProblem(404);

        // Delete
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ICategoryService service) =>
        {
            var result = await service.DeleteAsync(id);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(new { error = result.Message });
        })
        .WithName("DeleteCategory")
        .Produces(204)
        .ProducesProblem(404);
    }
}
```

### Step 8: Register Endpoints

In `Program.cs` or your module's endpoint registration:

```csharp
// After building the app
app.MapCategoryEndpoints();
```

### Step 9: Create Migration

```bash
cd src/Presentation/Gateway  # or your module's API project
dotnet ef migrations add AddCategoriesTable
dotnet ef database update
```

### Step 10: Test Your Feature

```bash
# Run the application
dotnet run

# Test in Swagger UI: https://localhost:5001/swagger
# Or use curl:

# Create
curl -X POST https://localhost:5001/api/categories \
  -H "Content-Type: application/json" \
  -d '{"name":"Electronics","description":"Electronic devices"}'

# Get all
curl https://localhost:5001/api/categories

# Get by ID
curl https://localhost:5001/api/categories/{id}
```

---

## Development Workflow

### Daily Workflow

1. **Pull latest changes**
   ```bash
   git pull origin main
   ```

2. **Create feature branch**
   ```bash
   git checkout -b feature/add-product-reviews
   ```

3. **Make changes** following the patterns above

4. **Run tests**
   ```bash
   dotnet test
   ```

5. **Build and verify**
   ```bash
   dotnet build
   ```

6. **Commit and push**
   ```bash
   git add .
   git commit -m "feat: Add product reviews feature"
   git push origin feature/add-product-reviews
   ```

7. **Create pull request**

### Code Quality Checklist

Before committing:
- ✅ All tests pass (`dotnet test`)
- ✅ Code builds without warnings (`dotnet build`)
- ✅ Used `Result<T>` pattern for all service methods
- ✅ Added structured logging
- ✅ Implemented caching where appropriate
- ✅ Used `AsNoTracking()` for read operations
- ✅ Added XML documentation comments
- ✅ Followed naming conventions

---

## Troubleshooting Common Issues

### Issue 1: Database Connection Failed

**Symptoms:**
```
SqlException: A connection attempt failed...
```

**Solutions:**
1. Check PostgreSQL is running: `docker ps` or `pg_isready`
2. Verify connection string in `appsettings.Development.json`
3. Ensure database exists: `psql -U postgres -c "\l"`

### Issue 2: Redis Connection Failed

**Symptoms:**
```
RedisConnectionException: It was not possible to connect to the redis server...
```

**Solutions:**
1. Disable Redis for local dev: Set `"EnableL2Cache": false` in appsettings
2. Or start Redis: `docker run -d -p 6379:6379 redis`

### Issue 3: Migration Fails

**Symptoms:**
```
Build failed. The following libraries failed to build:
```

**Solutions:**
1. Ensure you're in the correct directory (API project with DbContext reference)
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Check DbContext registration in DI

### Issue 4: Service Not Found (DI Error)

**Symptoms:**
```
InvalidOperationException: Unable to resolve service for type 'IProductService'
```

**Solutions:**
1. Ensure service is registered in `ServicesInstaller`
2. Check interface and implementation exist
3. Verify installer is called in `Program.cs`

### Issue 5: Cache Not Working

**Symptoms:**
- Data not being cached
- Stale data persisting

**Solutions:**
1. Check `CacheOptions.Enabled = true` in appsettings
2. Verify `ICacheService` is injected
3. Check cache invalidation on updates
4. Review cache key naming

---

## Next Steps

### Continue Learning

1. **Read Core Patterns:**
   - [Result Pattern Guide](../patterns/result-pattern-guide.md)
   - [Partial Class Override Pattern](../patterns/partial-class-pattern.md)
   - [Caching Strategy Guide](../patterns/caching-strategy.md)
   - [Testing Patterns Guide](../patterns/testing-patterns.md)

2. **Understand Migration:**
   - [VSA Migration Guide](./vsa-migration-guide.md)

3. **Explore Documentation Standards:**
   - [API Documentation Guide](./api-documentation.md)
   - [Logging Standards](../../docs/standards/logging-standards.md)
   - [OpenTelemetry Guide](../../docs/observability/opentelemetry-guide.md)

### Practice Tasks

1. Add validation to the Category service
2. Implement filtering and sorting in GetList
3. Add integration tests
4. Add OpenTelemetry tracing
5. Implement a relationship (e.g., Product has Category)

### Get Help

- Ask questions in team chat
- Review existing modules for examples
- Check documentation in `.ruru/docs/`
- Pair program with senior developers

---

**Last Updated**: 2025-11-20  
**Version**: 1.0  
**Author**: XFramework Development Team
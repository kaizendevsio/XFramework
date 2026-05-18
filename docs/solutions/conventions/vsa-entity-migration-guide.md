---
title: "VSA Entity Migration Guide"
date: 2026-05-15
category: conventions
module: XFramework
problem_type: convention
component: development_workflow
severity: high
applies_when:
  - "Migrating Domain.Shared entities into XFramework VSA entities with generated endpoints and mappings"
tags: [vsa, migration, entities, source-generators, endpoints]
---

# VSA Entity Migration Guide

## Overview

This guide provides detailed, step-by-step instructions for migrating existing Domain.Shared entities to the VSA (Vertical Slice Architecture) pattern using source generators.

**Related Documents:**
- [VSA Entity Placement Strategy](../architecture-patterns/vsa-entity-placement-strategy.md)
- [Attribute Usage Guide](../tooling-decisions/generate-endpoints-attribute-usage.md)
- [Auto-Discovery Guide](../tooling-decisions/generated-endpoint-auto-discovery.md)

## Prerequisites

Before beginning migration:

1. **Verify Source Generators Work**
   ```bash
   dotnet build src/Modules/XFramework.Inventario/Inventario.Api
   # Check for generated files in obj/Debug/net10.0/generated/
   ```

2. **Understand the Pattern**
   - Domain.Shared entities remain unchanged (pure domain)
   - VSA entities wrap domain entities in Api/Entities
   - Mappings convert between Domain and VSA
   - Generated code handles CRUD operations

3. **Review Working Example**
   - Study [`TestProduct.cs`](../../../src/Modules/XFramework.Inventario/Inventario.Api/Entities/TestProduct.cs)
   - Review the request DTOs in the same file

## Migration Levels

Choose the appropriate level based on entity complexity:

| Level | Complexity | Examples | Time Estimate |
|-------|-----------|----------|---------------|
| **Basic** | Simple entity, no relationships | Currency, WalletType | 15-30 min |
| **Standard** | Has relationships, basic logic | Product, Wallet, Message | 30-60 min |
| **Complex** | Many relationships, business logic | Order, User, Transaction | 1-2 hours |

## Basic Migration (Simple Entities)

### Example: Currency Entity

**Step 1: Locate Domain Entity**

```bash
# Find the domain entity
find src/Modules -name "Currency.cs" -path "*/Domain.Shared/*"
```

Example: `src/Shared/XFramework.Domain.Shared/Contracts/CurrencyType.cs`

**Step 2: Create VSA Entity**

Create `src/Modules/XFramework.Wallets/Wallets.Api/Entities/CurrencyEntity.cs`:

```csharp
using XFramework.Core.Attributes;

namespace Wallets.Api.Entities;

/// <summary>
/// VSA entity for Currency with auto-generated CRUD operations.
/// Maps to Domain.Shared.CurrencyType.
/// </summary>
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/currencies",
    RequireAuthorization = true,
    CacheDurationSeconds = 3600,  // Long cache for reference data
    CacheKeyPrefix = "currencies"
)]
public partial class CurrencyEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Request DTOs
public class CreateCurrencyEntityRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
}

public class UpdateCurrencyEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public bool IsActive { get; set; }
}

public class GetCurrencyEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool? IsActive { get; set; }
}
```

**Step 3: Create Mappings**

Create `src/Modules/XFramework.Wallets/Wallets.Api/Entities/CurrencyEntity.Mappings.cs`:

```csharp
using XFramework.Domain.Shared.Contracts;

namespace Wallets.Api.Entities;

/// <summary>
/// Mapping extensions between Domain.CurrencyType and CurrencyEntity.
/// </summary>
public static class CurrencyEntityMappings
{
    // Domain → VSA
    public static CurrencyEntity ToVsaEntity(this CurrencyType domain)
    {
        return new CurrencyEntity
        {
            Id = domain.Id,
            Code = domain.Code,
            Name = domain.Name,
            Symbol = domain.Symbol,
            DecimalPlaces = domain.DecimalPlaces,
            IsActive = domain.IsEnabled,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }
    
    // VSA → Domain
    public static CurrencyType ToDomainEntity(this CurrencyEntity vsa)
    {
        return new CurrencyType
        {
            Id = vsa.Id,
            Code = vsa.Code,
            Name = vsa.Name,
            Symbol = vsa.Symbol,
            DecimalPlaces = vsa.DecimalPlaces,
            IsEnabled = vsa.IsActive,
            CreatedAt = vsa.CreatedAt,
            ModifiedAt = vsa.UpdatedAt
        };
    }
}

/// <summary>
/// Partial implementation of CurrencyEntityService with mapping methods.
/// </summary>
public partial class CurrencyEntityService
{
    protected virtual partial CurrencyEntity MapCreateRequestToEntity(CreateCurrencyEntityRequest request)
    {
        var domain = new CurrencyType
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Symbol = request.Symbol,
            DecimalPlaces = request.DecimalPlaces,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        return domain.ToVsaEntity();
    }

    protected virtual partial void MapUpdateRequestToEntity(UpdateCurrencyEntityRequest request, CurrencyEntity entity)
    {
        entity.Name = request.Name;
        entity.Symbol = request.Symbol;
        entity.DecimalPlaces = request.DecimalPlaces;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    protected virtual partial IQueryable<CurrencyEntity> ApplyFilters(
        IQueryable<CurrencyEntity> query, 
        GetCurrencyEntityListRequest request)
    {
        if (request.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == request.IsActive.Value);
        }
        
        return query.OrderBy(c => c.Code);
    }
}
```

**Step 4: Build and Verify**

```bash
dotnet build src/Modules/XFramework.Wallets/Wallets.Api

# Verify generated files exist
ls src/Modules/XFramework.Wallets/Wallets.Api/obj/Debug/net10.0/generated/
# Should see: CurrencyEntityService.g.cs, CurrencyEntityEndpoints.g.cs
```

**Step 5: Enable Auto-Discovery**

Ensure `Program.cs` has auto-discovery enabled:

```csharp
using XFramework.Core.Extensions;

var builder = XApplication.Configure<Program>();

// Auto-discover services and endpoints
builder.Services.AddGeneratedServices();

var app = (WebApplication)builder.Build();

app.MapGeneratedEndpoints();

app.Run();
```

**Step 6: Test**

```bash
# Start the API
dotnet run --project src/Modules/XFramework.Wallets/Wallets.Api

# Test endpoints (in another terminal)
curl -X GET http://localhost:5000/api/currencies
curl -X GET http://localhost:5000/swagger  # Check Swagger UI
```

## Standard Migration (Entities with Relationships)

### Example: Product Entity

**Additional Considerations:**
- Handle navigation properties
- Decide on eager vs lazy loading
- Consider DTOs for related entities

**Step 1: Analyze Relationships**

```csharp
// Domain entity (examine relationships)
public partial class Product : BaseModel
{
    public string Name { get; set; }
    public Guid CategoryId { get; set; }
    public ProductCategory? Category { get; set; }  // Navigation property
    public List<ProductVariation>? Variations { get; set; }  // Collection
}
```

**Step 2: Create VSA Entity (Flatten Relationships)**

```csharp
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/products"
)]
public partial class ProductEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    
    // Flatten relationship to ID only (for write operations)
    public Guid CategoryId { get; set; }
    
    // Optionally include related data (for read operations)
    public string? CategoryName { get; set; }
    
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Step 3: Handle Navigation Loading**

```csharp
public partial class ProductEntityService
{
    protected virtual partial IQueryable<ProductEntity> ApplyFilters(
        IQueryable<ProductEntity> query, 
        GetProductEntityListRequest request)
    {
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(p => 
                p.Name.Contains(request.SearchTerm) || 
                p.Description.Contains(request.SearchTerm));
        }
        
        // Filter by category
        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }
        
        // Filter by price range
        if (request.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= request.MinPrice.Value);
        }
        
        if (request.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= request.MaxPrice.Value);
        }
        
        return query.OrderBy(p => p.Name);
    }
}
```

**Step 4: Enrich with Related Data**

Consider creating a custom endpoint for detailed product info:

```csharp
// In ProductEntityEndpoints.cs (manual addition)
public static class ProductEntityEndpointsExtensions
{
    public static IEndpointRouteBuilder MapProductWithDetailsEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{id}/details", async (
            Guid id,
            IProductEntityService service,
            IProductCategoryEntityService categoryService,
            CancellationToken ct) =>
        {
            var product = await service.GetByIdAsync(id, includeNavigations: true, ct: ct);
            if (!product.IsSuccess) return Results.NotFound();
            
            // Enrich with category details
            var category = await categoryService.GetByIdAsync(product.Data!.CategoryId, ct: ct);
            
            var dto = new ProductDetailsDto
            {
                Product = product.Data,
                Category = category.Data
            };
            
            return Results.Ok(dto);
        })
        .WithName("GetProductDetails")
        .WithTags("Products");
        
        return app;
    }
}
```

## Complex Migration (Business Logic & Validations)

### Example: WalletTransaction Entity

**Additional Considerations:**
- Preserve existing business logic
- Maintain validation rules
- Handle transaction boundaries
- Keep audit logging

**Step 1: Identify Business Logic**

```csharp
// Existing custom service (keep this!)
public interface IWalletService
{
    Task<Result> IncrementBalanceAsync(Guid walletId, decimal amount);
    Task<Result> DecrementBalanceAsync(Guid walletId, decimal amount);
    Task<Result> TransferAsync(Guid fromWalletId, Guid toWalletId, decimal amount);
}
```

**Step 2: Create VSA Entity for CRUD Only**

```csharp
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Get | EndpointActions.GetList,  // READ ONLY!
    RoutePrefix = "api/wallet-transactions"
)]
public partial class WalletTransactionEntity
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Step 3: Keep Custom Service for Write Operations**

```csharp
// Keep existing WalletService.cs (no changes)
public class WalletService : IWalletService
{
    public async Task<Result> IncrementBalanceAsync(Guid walletId, decimal amount)
    {
        // Existing business logic preserved
        // Validation, transaction boundary, audit logging, etc.
    }
}
```

**Step 4: Coexist VSA and Custom Services**

```csharp
// Program.cs - both are registered
builder.Services.AddGeneratedServices();  // Registers WalletTransactionEntityService (read-only)
builder.Services.AddScoped<IWalletService, WalletService>();  // Custom logic for writes
```

**Usage:**
- **Read Operations**: Use generated `WalletTransactionEntityService` (simpler, consistent)
- **Write Operations**: Use custom `WalletService` (preserves business logic)

## Module-Specific Patterns

### Inventario Module

**Entities to Migrate:**
1. `Product` → `ProductEntity` (Standard)
2. `ProductCategory` → `ProductCategoryEntity` (Basic)
3. `ProductVariation` → `ProductVariationEntity` (Standard)
4. `ProductTransaction` → `ProductTransactionEntity` (Read-only)

**Special Considerations:**
- Product has category relationship
- ProductVariation has product relationship
- ProductTransaction should be read-only (use custom service for stock changes)

### Wallets Module

**Entities to Migrate:**
1. `Wallet` → `WalletEntity` (Read-only - custom service for balance changes)
2. `WalletType` → `WalletTypeEntity` (Basic)
3. `WalletTransaction` → `WalletTransactionEntity` (Read-only)
4. `Currency` → `CurrencyEntity` (Basic, reference data)

**Special Considerations:**
- Keep existing `IWalletService` for all balance modifications
- VSA entities only for read operations and lookups
- Maintain transaction boundary integrity

### IdentityServer Module

**Entities to Migrate:**
1. `IdentityInformation` → Read-only, keep custom service
2. `IdentityRole` → `IdentityRoleEntity` (Standard)
3. `IdentityCredential` → Read-only, keep custom authentication service
4. `Session` → Read-only VSA entity
5. `AuthorizationLog` → Read-only VSA entity (audit data)

**Special Considerations:**
- Authentication logic stays in custom services
- Authorization requires careful review
- Audit logs should be append-only (no Update/Delete)

## Troubleshooting

### Issue: Generated Files Not Appearing

**Symptoms:**
- No `*Service.g.cs` or `*Endpoints.g.cs` files in obj folder
- Build succeeds but no code generated

**Solutions:**

1. **Verify Source Generator Reference:**
```xml
<ProjectReference Include="..\..\SourceGenerators\XFramework.SourceGenerators\XFramework.SourceGenerators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false"/>
```

2. **Enable Generated Files Output:**
```xml
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

3. **Clean and Rebuild:**
```bash
dotnet clean
dotnet build -v:diag | grep SourceGenerator
```

### Issue: Compilation Errors in Generated Code

**Symptoms:**
- Build fails with errors in generated files
- Missing type references

**Solutions:**

1. **Check Attribute Application:**
```csharp
// Ensure GenerateEndpointsAttribute is applied
[GenerateEndpoints(...)]  // Must be present
public partial class ProductEntity  // Must be partial
```

2. **Verify Required Properties:**
```csharp
// Entity must have Id property
public Guid Id { get; set; }  // Required
```

3. **Check Request DTOs Exist:**
```csharp
// These must be defined
public class CreateProductEntityRequest { }
public class UpdateProductEntityRequest { }
public class GetProductEntityListRequest 
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
```

### Issue: Endpoints Not Showing in Swagger

**Symptoms:**
- Build succeeds
- Generated code present
- Endpoints don't appear in Swagger UI

**Solutions:**

1. **Verify Auto-Discovery:**
```csharp
app.MapGeneratedEndpoints();  // Must be called
```

2. **Check Endpoint Method Signature:**
```csharp
// Generated method should match:
public static IEndpointRouteBuilder MapProductEntityEndpoints(
    this IEndpointRouteBuilder app)
```

3. **Enable Diagnostic Logging:**
```csharp
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
// Look for "Auto-discovery" messages in logs
```

### Issue: Service Injection Fails

**Symptoms:**
- Runtime error: "Unable to resolve service for type 'IProductEntityService'"

**Solutions:**

1. **Verify Auto-Registration:**
```csharp
builder.Services.AddGeneratedServices();  // Must be before Build()
```

2. **Check Interface Naming:**
```csharp
// Generated interface: IProductEntityService
// Generated class: ProductEntityService
// Names must match pattern
```

3. **Manual Registration (Fallback):**
```csharp
builder.Services.AddScoped<IProductEntityService, ProductEntityService>();
```

## Validation Checklist

Use this checklist to verify successful migration:

### Pre-Migration
- [ ] Source generators build successfully
- [ ] Test entities work (TestProduct, etc.)
- [ ] Auto-discovery is enabled in Program.cs
- [ ] Domain entity exists in Domain.Shared

### Per-Entity Migration
- [ ] VSA entity created in Module.Api/Entities/
- [ ] `[GenerateEndpoints]` attribute applied with correct parameters
- [ ] Entity class is `partial`
- [ ] Entity has `Id` property
- [ ] Request DTOs defined (Create, Update, GetList)
- [ ] Mappings file created (*.Mappings.cs)
- [ ] `MapCreateRequestToEntity` implemented
- [ ] `MapUpdateRequestToEntity` implemented
- [ ] `ApplyFilters` implemented
- [ ] Build succeeds without errors
- [ ] Generated files appear in obj/Debug/net10.0/generated/

### Post-Migration Verification
- [ ] Service registered (check startup logs)
- [ ] Endpoints appear in Swagger
- [ ] GET /api/entities returns data
- [ ] GET /api/entities/{id} returns single entity
- [ ] POST /api/entities creates entity
- [ ] PUT /api/entities/{id} updates entity
- [ ] DELETE /api/entities/{id} deletes entity (if enabled)
- [ ] Authorization works correctly
- [ ] Caching works (if enabled)
- [ ] All existing tests pass
- [ ] Performance acceptable

### Documentation
- [ ] Migration notes added to module README
- [ ] Team notified of changes
- [ ] Any custom logic documented
- [ ] Mapping logic reviewed

## Tips and Best Practices

### 1. Start Simple
Begin with basic entities (no relationships, simple properties) to get comfortable with the pattern before tackling complex entities.

### 2. Use Consistent Naming
Stick to either "Entity" suffix or same-name-different-namespace consistently across your module.

### 3. Keep Domain Pure
Never modify Domain.Shared entities. All infrastructure concerns stay in VSA layer.

### 4. Map Efficiently
Consider using AutoMapper or Mapperly for complex mappings to reduce boilerplate.

### 5. Test Incrementally
Test each entity after migration rather than migrating all entities and testing at the end.

### 6. Preserve Business Logic
Keep existing custom services for complex business logic. Use VSA for simple CRUD only.

### 7. Document Decisions
Add comments explaining why certain operations are excluded or why custom services are retained.

### 8. Monitor Performance
Compare response times before and after migration. Investigate any regressions.

## Next Steps

After completing migration:

1. **Remove Old Code**: Delete manual CRUD implementations that have been replaced
2. **Update Documentation**: Ensure all API docs reference new endpoints
3. **Performance Review**: Analyze response times and optimize if needed
4. **Team Training**: Conduct session on VSA pattern and best practices
5. **Continuous Improvement**: Gather feedback and refine the pattern

## Getting Help

If you encounter issues not covered in this guide:

1. Review [VSA Entity Placement Strategy](../architecture-patterns/vsa-entity-placement-strategy.md) for architectural decisions
2. Check [Auto-Discovery Guide](../tooling-decisions/generated-endpoint-auto-discovery.md) for discovery issues
3. Examine working example: [`TestProduct.cs`](../../../src/Modules/XFramework.Inventario/Inventario.Api/Entities/TestProduct.cs)
4. Enable diagnostic logging and review build output
5. Consult with Technical Architect

---

**Version**: 1.0  
**Last Updated**: 2025-11-25  
**Author**: Technical Architect

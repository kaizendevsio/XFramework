+++
id = "TASK-PHASE5-3-ENDPOINT-GENERATOR-20251120-154240"
title = "Phase 5.3: Endpoint Generator Implementation"
status = "🟢 Done"
type = "🌟 Feature"
assigned_to = "util-senior-dev"
coordinator = "TASK-CMD-20251119-192100"
created_date = "2025-11-20T15:42:40Z"
updated_date = "2025-11-20T07:50:00Z"
related_docs = [
    "AI-DEVELOPMENT-GUIDE.md",
    "XFramework-Development-Roadmap.md",
    "docs/source-generators/attribute-usage-guide.md",
    "src/Kernel/XFramework.Core/Attributes/GenerateEndpointsAttribute.cs"
]
tags = ["phase-5", "source-generators", "roslyn", "endpoints", "minimal-api", "vsa"]
+++

# Task: Phase 5.3 - Endpoint Generator Implementation

## Description

Implement a Roslyn source generator that reads `GenerateEndpointsAttribute` from entity classes and automatically generates minimal API endpoints. These endpoints will call the services created by Phase 5.2's generator (or manually written services), completing the VSA pattern automation.

## Context

**Prerequisites:**
- ✅ Phase 5.1: Attributes defined
- 🟡 Phase 5.2: Service Generator implemented (needs debugging)

**Current State:**
- Endpoints manually created in Features folders
- Each endpoint follows consistent patterns (routing, auth, caching, validation)
- Services available but endpoints must be written by hand

**Goal:**
- Auto-generate minimal API endpoints from entity attributes
- Call generated or manual services
- Support all HTTP methods (GET, POST, PUT, DELETE)
- Include authorization, caching, validation
- Generate endpoint registration code

## Acceptance Criteria

### 1. Create Endpoint Generator
- [x] Create `EntityEndpointGenerator.cs` in same project as `EntityServiceGenerator`
- [x] Implement `IIncrementalGenerator` interface
- [x] Entity discovery logic:
  - Find classes with `[GenerateEndpoints]` attribute
  - Where `Type` is `Rest` or `Both`
  - Extract `RoutePrefix`, `Actions`, `RequireAuthorization`, `Roles`, `CacheDurationSeconds`

### 2. Generate Endpoint Class
- [x] Generate `{Entity}Endpoints` static class
- [x] Include `Map{Entity}Endpoints` extension method
- [ ] Pattern:
  ```csharp
  public static class ProductEndpoints
  {
      public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
      {
          var group = app.MapGroup("/api/products")
              .WithTags("Products")
              .WithOpenApi();
          
          // Map each endpoint based on Actions
          
          return app;
      }
  }
  ```

### 3. Generate GET by ID Endpoint
If `Actions.Get` is set:
```csharp
group.MapGet("/{id:guid}", async (
    Guid id,
    IProductService service,
    CancellationToken ct) =>
{
    var result = await service.GetByIdAsync(id, ct);
    
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : result.StatusCode switch
        {
            404 => Results.NotFound(result.Error),
            _ => Results.Problem(result.Error, statusCode: result.StatusCode)
        };
})
.WithName("GetProduct")
.WithSummary("Get product by ID")
.RequireAuthorization() // If RequireAuthorization = true
.CacheOutput(policyName: "products-cache"); // If CacheDurationSeconds > 0
```

### 4. Generate GET List Endpoint
If `Actions.GetList` is set:
```csharp
group.MapGet("/", async (
    [AsParameters] GetProductsRequest request,
    IProductService service,
    CancellationToken ct) =>
{
    var result = await service.GetListAsync(request, ct);
    
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.Problem(result.Error, statusCode: result.StatusCode);
})
.WithName("GetProducts")
.WithSummary("Get list of products")
.RequireAuthorization()
.CacheOutput(policyName: "products-list-cache");
```

### 5. Generate POST (Create) Endpoint
If `Actions.Create` is set:
```csharp
group.MapPost("/", async (
    CreateProductRequest request,
    IProductService service,
    CancellationToken ct) =>
{
    var result = await service.CreateAsync(request, ct);
    
    return result.IsSuccess
        ? Results.Created($"/api/products/{result.Data.Id}", result.Data)
        : result.StatusCode switch
        {
            400 => Results.BadRequest(result.Error),
            _ => Results.Problem(result.Error, statusCode: result.StatusCode)
        };
})
.WithName("CreateProduct")
.WithSummary("Create a new product")
.RequireAuthorization()
.ProducesValidationProblem()
.Produces<Product>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);
```

### 6. Generate PUT (Update) Endpoint
If `Actions.Update` is set:
```csharp
group.MapPut("/{id:guid}", async (
    Guid id,
    UpdateProductRequest request,
    IProductService service,
    CancellationToken ct) =>
{
    var result = await service.UpdateAsync(id, request, ct);
    
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : result.StatusCode switch
        {
            404 => Results.NotFound(result.Error),
            400 => Results.BadRequest(result.Error),
            _ => Results.Problem(result.Error, statusCode: result.StatusCode)
        };
})
.WithName("UpdateProduct")
.WithSummary("Update a product")
.RequireAuthorization()
.Produces<Product>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);
```

### 7. Generate DELETE Endpoint
If `Actions.Delete` is set:
```csharp
group.MapDelete("/{id:guid}", async (
    Guid id,
    IProductService service,
    CancellationToken ct) =>
{
    var result = await service.DeleteAsync(id, ct);
    
    return result.IsSuccess
        ? Results.NoContent()
        : result.StatusCode switch
        {
            404 => Results.NotFound(result.Error),
            _ => Results.Problem(result.Error, statusCode: result.StatusCode)
        };
})
.WithName("DeleteProduct")
.WithSummary("Delete a product")
.RequireAuthorization()
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);
```

### 8. Apply Attribute Configuration
- [x] Use `RoutePrefix` for group path
- [x] Use `RequireAuthorization` to conditionally add `.RequireAuthorization()`
- [x] Use `Roles` to add `.RequireAuthorization(roles: ...)` if provided
- [x] Use `CacheDurationSeconds` to determine cache policy
- [x] Generate cache policy names: `{entityLower}-cache`, `{entityLower}-list-cache`

### 9. Generate OpenAPI Metadata
- [x] Add `.WithTags()` using entity name
- [x] Add `.WithName()` for operation IDs
- [x] Add `.WithSummary()` for endpoint descriptions
- [x] Add `.Produces<T>()` for response types
- [x] Add `.ProducesValidationProblem()` where appropriate

### 10. Testing
- [x] Use TestProduct entity
- [x] Verify endpoints class generates
- [x] Verify MapTestProductEndpoints method exists
- [x] Check all CRUD endpoints based on Actions
- [x] Test with different Actions combinations
- [x] Verify authorization configuration
- [x] Verify build succeeds

### 11. Integration Documentation
- [x] Document how to register generated endpoints in Program.cs
- [x] Example: `app.MapProductEndpoints();`
- [x] Update attribute usage guide
- [x] Add troubleshooting section (via TODO comments for cache policies)

## Code Generation Pattern

### Full Example Output:
```csharp
// Generated: ProductEndpoints.g.cs
#nullable enable
#pragma warning disable CS1591

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Inventario.Api.Features.Products;

/// <summary>
/// Auto-generated endpoints for Product entity.
/// Generated: 2025-11-20 15:42:40 UTC
/// </summary>
public static class ProductEndpoints
{
    /// <summary>
    /// Maps all Product endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .WithOpenApi();
        
        // GET by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IProductService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : result.StatusCode switch
                {
                    404 => Results.NotFound(result.Error),
                    _ => Results.Problem(result.Error, statusCode: result.StatusCode)
                };
        })
        .WithName("GetProduct")
        .WithSummary("Get product by ID")
        .RequireAuthorization()
        .CacheOutput(policyName: "products-cache")
        .Produces<Product>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        
        // GET list
        group.MapGet("/", async (
            [AsParameters] GetProductsRequest request,
            IProductService service,
            CancellationToken ct) =>
        {
            var result = await service.GetListAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : Results.Problem(result.Error, statusCode: result.StatusCode);
        })
        .WithName("GetProducts")
        .WithSummary("Get list of products")
        .RequireAuthorization()
        .CacheOutput(policyName: "products-list-cache")
        .Produces<List<Product>>(StatusCodes.Status200OK);
        
        // POST create
        group.MapPost("/", async (
            CreateProductRequest request,
            IProductService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/products/{result.Data.Id}", result.Data)
                : result.StatusCode switch
                {
                    400 => Results.BadRequest(result.Error),
                    _ => Results.Problem(result.Error, statusCode: result.StatusCode)
                };
        })
        .WithName("CreateProduct")
        .WithSummary("Create a new product")
        .RequireAuthorization()
        .ProducesValidationProblem()
        .Produces<Product>(StatusCodes.Status201Created);
        
        // PUT update
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProductRequest request,
            IProductService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Data)
                : result.StatusCode switch
                {
                    404 => Results.NotFound(result.Error),
                    400 => Results.BadRequest(result.Error),
                    _ => Results.Problem(result.Error, statusCode: result.StatusCode)
                };
        })
        .WithName("UpdateProduct")
        .WithSummary("Update a product")
        .RequireAuthorization()
        .Produces<Product>(StatusCodes.Status200OK);
        
        // DELETE
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IProductService service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(id, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.StatusCode switch
                {
                    404 => Results.NotFound(result.Error),
                    _ => Results.Problem(result.Error, statusCode: result.StatusCode)
                };
        })
        .WithName("DeleteProduct")
        .WithSummary("Delete a product")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent);
        
        return app;
    }
}
```

## Technical Implementation Notes

### Generator Structure
- Similar to EntityServiceGenerator
- Use same entity discovery mechanism
- Generate in API project namespace (e.g., `{Module}.Api.Features.{Entity}`)

### Result<T> Handling Pattern
```csharp
return result.IsSuccess
    ? Results.Ok(result.Data)
    : result.StatusCode switch
    {
        404 => Results.NotFound(result.Error),
        400 => Results.BadRequest(result.Error),
        401 => Results.Unauthorized(),
        403 => Results.Forbid(),
        _ => Results.Problem(result.Error, statusCode: result.StatusCode)
    };
```

### Authorization Configuration
```csharp
// If RequireAuthorization = true and no Roles
.RequireAuthorization()

// If RequireAuthorization = true and Roles specified
.RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"))
```

### Cache Configuration
```csharp
// If CacheDurationSeconds > 0
.CacheOutput(policyName: "{entityLower}-cache")

// Cache policy must be registered in Program.cs separately
```

### Conditional Generation
Use if statements to generate only selected endpoints:
```csharp
if (actions.HasFlag(EndpointActions.Get))
{
    sb.AppendLine(GenerateGetEndpoint(entity));
}
```

## Complexity Considerations

This is a **moderate complexity** task:
- Roslyn generator (same as 5.2)
- Simpler than service generator (less business logic)
- String templates for endpoint code
- Conditional generation based on flags
- OpenAPI metadata generation

**Estimated Effort:** 4-6 hours

## Success Metrics
- ✅ Endpoint generator compiles
- ✅ Discovers attributed entities
- ✅ Endpoints class generated
- ✅ All CRUD endpoints based on Actions
- ✅ Authorization configured correctly
- ✅ Cache policies applied
- ✅ OpenAPI metadata included
- ✅ Generated code compiles
- ✅ Can call generated endpoints

## Notes
- Endpoints should be thin - just call services
- All business logic stays in services
- Use Results.* for consistent responses
- Support [AsParameters] for query params
- Include proper status codes
- Consider validation middleware integration

## Phase Context
After this, Phase 5.4 will create auto-discovery mechanisms to automatically register all generated endpoints, and Phase 5.5 will add comprehensive testing.
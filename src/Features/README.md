# Features Directory - Vertical Slice Architecture

This root-level directory is historical guidance. Current API features live under module projects, for example `src/Modules/XFramework.Wallets/Wallets.Api/Features/` and `src/Modules/XFramework.Communications/Communications.Api/Features/`.

Use this file only as a lightweight VSA orientation. For current implementation rules, use `docs/solutions/conventions/xframework-vsa-agent-playbook.md` and `docs/solutions/conventions/xframework-best-practices.md`.

## Directory Structure

Each feature is organized by **domain entity** -> **action**:

```
src/Modules/XFramework.[Module]/[Module].Api/Features/
|-- Products/
|   |-- Get/
|   |   `-- Endpoint.cs       # Static handler with [MapGet] and optional [BoltHandler]
|   |-- Create/
|   |   |-- Endpoint.cs       # Static handler with [MapPost] and optional [BoltHandler]
|   |   `-- Validator.cs      # FluentValidation (optional)
|   |-- Update/
|   |   `-- ...
|   `-- Delete/
|       `-- ...
`-- Users/
    `-- ...
```

## Naming Conventions

### Folders
- **Use action verbs**: `Get/`, `Create/`, `Update/`, `Delete/`, `List/`
- **Singular entity names**: `Product/`, not `Products/`
- **Nested by domain**: `Orders/Items/Get/` for order items

### Files
- **Generic endpoint names**: `Endpoint.cs`; validators usually use action-specific names such as `CreateProductValidator.cs`
- **Namespace provides context**: `Features.Products.Get` makes it clear this is "Get Product"
- **Cleaner imports**: `using Features.Products.Get;` vs `using Features.Products.GetProduct;`

## File Templates

### Endpoint.cs (Generated Minimal API + Optional Bolt)

```csharp
namespace Features.Products.Get;

public static class GetProductEndpoint
{
    [MapGet("/api/products/{id:guid}", Tags = ["Products"], Summary = "Get product")]
    [BoltHandler]
    public static async Task<Result<ProductResponse>> Handle(
        GetProductRequest request,
        ProductService service,
        CancellationToken ct)
    {
        var result = await service.GetAsync(request.Id, ct);

        return result.IsSuccess ? result : Result<ProductResponse>.NotFound("Product not found");
    }
}

public sealed record GetProductRequest(Guid Id) :
    IBoltRequest<GetProductRequest, Result<ProductResponse>>;
```

`Program.cs` maps generated feature routes once with `app.MapGeneratedEndpoints()`. Keep explicit manual mappings only for endpoints that need custom parameter binding or result handling that the generator cannot express.

### Service.cs (Optional - for complex logic)

```csharp
namespace Features.Products.Create;

public sealed class ProductService(AppDbContext db, ILogger<ProductService> logger)
{
    public async Task<Result<Product>> ExecuteAsync(CreateRequest request, CancellationToken ct)
    {
        // Complex business logic here
        var product = new Product { ... };
        
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        
        return Result.Success(product);
    }
}
```

### Validator.cs (FluentValidation)

```csharp
namespace Features.Products.Create;

public class Validator : AbstractValidator<CreateRequest>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}
```

## Core Principles

### 1. Direct Service Injection (No MediatR)
**Do**: Inject services directly into endpoints
```csharp
public static class CreateProductEndpoint
{
    [MapPost("/api/products", Tags = ["Products"])]
    public static Task<Result<ProductResponse>> Handle(
        CreateProductRequest request,
        ProductService service,
        CancellationToken ct) => service.CreateAsync(request, ct);
}
```

**Do not**: Use MediatR
```csharp
// Removed - no more MediatR
app.MapPost("/api/products", async (IMediator mediator, Product model) =>
{
    return await mediator.Send(new Create<Product>(model));
});
```

### 2. Use Result<T> Pattern
All service methods return `Result<T>` for consistent error handling:

```csharp
public async Task<Result<Product>> CreateAsync(Product entity, CancellationToken ct)
{
    try
    {
        _db.Products.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Result.Success(entity);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create product");
        return Result.Failure<Product>("Failed to create product");
    }
}
```

### 3. Feature Isolation
- Each feature folder contains ALL code for that specific action
- No shared handlers or generic CRUD classes
- Business logic lives close to the endpoint

### 4. Simplicity Over Abstraction
- **Simple features stay simple**: Just `Endpoint.cs` for basic CRUD
- **Complex features can elaborate**: Add service methods and validators as needed
- **No unnecessary layers**: Direct DbContext access when appropriate

## Migration from CQRS

This section is historical migration guidance. Do not introduce CQRS/MediatR in new features.

### Before (CQRS/MediatR)
```
DataAccess/
  Commands/
    CreateHandler.cs      # Generic handler for all entities
    Create.cs             # Command record
  Query/
    GetHandler.cs         # Generic handler for all entities
    Get.cs                # Query record
```

### After (VSA)
```
Features/
  Products/
    Create/
      Endpoint.cs         # Direct endpoint, no handler needed
    Get/
      Endpoint.cs         # Direct endpoint, no handler needed
```

**Benefits**:
- 40-60% less code
- Easier to understand and maintain
- Faster to implement new features
- Better testability

## Reference Files

- **AI Development Guide**: `docs/solutions/conventions/xframework-vsa-agent-playbook.md`
- **Best Practices**: `docs/solutions/conventions/xframework-best-practices.md`
- **Generated Endpoint Registration**: `docs/solutions/tooling-decisions/generated-endpoint-auto-discovery.md`
- **GenerateEndpoints Attribute**: `docs/solutions/tooling-decisions/generate-endpoints-attribute-usage.md`

## Best Practices

1. **Keep features focused**: One feature = One user action
2. **Co-locate related code**: All code for a feature in its folder
3. **Minimize dependencies**: Inject only what's needed
4. **Use generated results appropriately**: return `Result<T>` from `[Map*]` generated handlers; reserve typed HTTP results for fully manual endpoints
5. **Apply caching strategically**: follow `docs/solutions/best-practices/xframework-caching-strategy.md`
6. **Log appropriately**: Use structured logging for important events
7. **Validate early**: Use FluentValidation for complex validation
8. **Test thoroughly**: Unit test services, integration test endpoints

---

**Last Updated**: 2026-05-21 | **Version**: 1.1

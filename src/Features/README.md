# Features Directory - Vertical Slice Architecture

This directory contains all feature implementations using **Vertical Slice Architecture (VSA)**.

## 📁 Directory Structure

Each feature is organized by **domain entity** → **action**:

```
Features/
├── Products/
│   ├── Get/
│   │   ├── Endpoint.cs       # Minimal API endpoint
│   │   └── Query.cs           # Query logic (optional)
│   ├── Create/
│   │   ├── Endpoint.cs       # Minimal API endpoint
│   │   ├── Validator.cs      # FluentValidation (optional)
│   │   └── Command.cs         # Command logic (optional)
│   ├── Update/
│   │   └── ...
│   └── Delete/
│       └── ...
└── Users/
    └── ...
```

## 🎯 Naming Conventions

### Folders
- **✅ Use action verbs**: `Get/`, `Create/`, `Update/`, `Delete/`, `List/`
- **✅ Singular entity names**: `Product/`, not `Products/`
- **✅ Nested by domain**: `Orders/Items/Get/` for order items

### Files
- **✅ Generic names**: `Endpoint.cs`, `Query.cs`, `Command.cs`, `Validator.cs`
- **✅ Namespace provides context**: `Features.Products.Get` makes it clear this is "Get Product"
- **✅ Cleaner imports**: `using Features.Products.Get;` vs `using Features.Products.GetProduct;`

## 📝 File Templates

### Endpoint.cs (Minimal API)

```csharp
namespace Features.Products.Get;

public static class Endpoint
{
    public record Request(Guid Id);
    public record Response(Guid Id, string Name, decimal Price);
    
    public static async Task<Results<Ok<Response>, NotFound>> Handle(
        [AsParameters] Request request,
        ProductService service,
        CancellationToken ct)
    {
        var result = await service.GetAsync(request.Id, ct);
        
        return result.IsSuccess
            ? TypedResults.Ok(new Response(result.Data.Id, result.Data.Name, result.Data.Price))
            : TypedResults.NotFound();
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

### Query.cs / Command.cs (Optional - for complex logic)

```csharp
namespace Features.Products.Create;

public class Command
{
    private readonly AppDbContext _db;
    private readonly ILogger<Command> _logger;
    
    public Command(AppDbContext db, ILogger<Command> logger)
    {
        _db = db;
        _logger = logger;
    }
    
    public async Task<Result<Product>> ExecuteAsync(CreateRequest request, CancellationToken ct)
    {
        // Complex business logic here
        var product = new Product { ... };
        
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        
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

## 🔑 Core Principles

### 1. Direct Service Injection (No MediatR)
**✅ DO**: Inject services directly into endpoints
```csharp
app.MapPost("/api/products", async (ProductService service, Product model) =>
{
    var result = await service.CreateAsync(model);
    return result.IsSuccess ? Results.Created(...) : Results.BadRequest(...);
});
```

**❌ DON'T**: Use MediatR
```csharp
// ❌ REMOVED - No more MediatR
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
- **Complex features can elaborate**: Add `Command.cs`, `Validator.cs` as needed
- **No unnecessary layers**: Direct DbContext access when appropriate

## 🚀 Migration from CQRS

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
- ✅ 40-60% less code
- ✅ Easier to understand and maintain
- ✅ Faster to implement new features
- ✅ Better testability

## 📚 Reference Files

- **AI Development Guide**: `AI-DEVELOPMENT-GUIDE.md`
- **Full Roadmap**: `XFramework-Development-Roadmap.md`
- **Architecture Plan**: `XFramework-Improvement-Plan.md`

## 🎨 Best Practices

1. **Keep features focused**: One feature = One user action
2. **Co-locate related code**: All code for a feature in its folder
3. **Minimize dependencies**: Inject only what's needed
4. **Use typed results**: Leverage `Results<Ok<T>, NotFound, BadRequest>`
5. **Apply caching strategically**: Add `CacheOutput` for read operations
6. **Log appropriately**: Use structured logging for important events
7. **Validate early**: Use FluentValidation for complex validation
8. **Test thoroughly**: Unit test services, integration test endpoints

---

**Last Updated**: 2025-11-19 | **Version**: 1.0
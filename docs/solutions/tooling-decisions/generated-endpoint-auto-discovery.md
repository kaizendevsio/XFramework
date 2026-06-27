---
title: "Generated Endpoint Auto-Discovery and Registration"
date: 2026-05-21
category: tooling-decisions
module: XFramework.SourceGenerators
problem_type: tooling_decision
component: tooling
severity: medium
applies_when:
  - "Replacing manual Program.cs service and endpoint registrations with generated discovery extensions"
tags: [source-generators, auto-discovery, registration, endpoints, services]
---

# Auto-Discovery & Registration Guide

This guide owns generated endpoint and service registration mechanics in XFramework: `MapGeneratedEndpoints()`, `MapEndpoint<TEndpoint>()`, `AddGeneratedServices()`, and discovery opt-outs. Entity declaration options live in [GenerateEndpoints Attribute Usage Guide](./generate-endpoints-attribute-usage.md). Cache key, invalidation, and runtime cache behavior live in [XFramework Caching Strategy](../best-practices/xframework-caching-strategy.md).

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Endpoint Auto-Discovery](#endpoint-auto-discovery)
- [Validator Auto-Detection](#validator-auto-detection)
- [Service Auto-Discovery](#service-auto-discovery)
- [Opt-Out Mechanism](#opt-out-mechanism)
- [Performance Considerations](#performance-considerations)
- [Troubleshooting](#troubleshooting)
- [Advanced Usage](#advanced-usage)

## Overview

### The Problem

Before auto-discovery, every entity required manual registration:

```csharp
// Program.cs - Before
var app = builder.Build();

// Manual registration for each entity
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapCustomerEndpoints();
app.MapInvoiceEndpoints();
// ... dozens more

// Service registration
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
// ... dozens more

app.Run();
```

**Problems:**
- Scales poorly (N entities = 2N manual registrations)
- Easy to forget when adding new entities
- Code clutter in `Program.cs`
- Difficult to maintain

### The Solution

With auto-discovery, a single call maps generated endpoint routes:

```csharp
// Program.cs - After
// Auto-discover and register generated services where the module uses service discovery
builder.Services.AddGeneratedServices();

var app = builder.Build();

// Auto-discover and map generated endpoints
app.MapGeneratedEndpoints();

app.Run();
```

**Benefits:**
- Zero manual registrations needed
- Automatic when generated endpoint classes are present from `[GenerateEndpoints]` entities or `[Map*]` feature handlers
- Clean, maintainable `Program.cs`
- <100ms startup overhead
- Opt-out available when needed

## Quick Start

### Step 1: Apply Attribute to Entity

```csharp
using XFramework.Core.Attributes;

[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/products"
)]
public partial class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

### Step 2: Update Program.cs

Replace manual registrations with auto-discovery:

```csharp
using XFramework.Core.Extensions;

var builder = XApplication.Configure<Program>();

// Add auto-discovered services
builder.Services.AddGeneratedServices();

var app = (WebApplication)builder.Build();

// Map auto-discovered endpoints
app.MapGeneratedEndpoints();

app.Run();
```

### Step 3: Build and Run

```bash
dotnet build
dotnet run
```

The source generator creates the service and endpoints, and auto-discovery registers them automatically!

## Endpoint Auto-Discovery

### How It Works

The `MapGeneratedEndpoints()` method:

1. **Scans Assemblies**: Looks through loaded assemblies matching the filter
2. **Finds Endpoint Classes**: Identifies types ending with "Endpoints"
3. **Locates Map Methods**: Finds static methods matching pattern `Map{TypeName}Endpoints(IEndpointRouteBuilder)`
4. **Invokes Methods**: Calls each method via reflection to register endpoints
5. **Caches Results**: Stores discovered types for subsequent calls

Current module `Program.cs` files typically call `app.MapGeneratedEndpoints()` after middleware and health/documentation mapping. Some modules also keep explicit manual mappings for endpoints with custom parameter binding or custom `IResult` flows.

Representative current modules:
- `src/Modules/XFramework.IdentityServer/IdentityServer.Api/Program.cs`
- `src/Modules/XFramework.Wallets/Wallets.Api/Program.cs`
- `src/Modules/XFramework.Communications/Communications.Api/Program.cs`

### Basic Usage

```csharp
// Default: Scans XFramework.*, Inventario.*, Wallets.*, Communications.* assemblies
app.MapGeneratedEndpoints();
```

### Custom Assembly Filter

```csharp
// Only scan specific assemblies
app.MapGeneratedEndpoints(asm => 
    asm.FullName?.Contains("MyProject") == true);

// Scan multiple patterns
app.MapGeneratedEndpoints(asm => {
    var name = asm.FullName ?? string.Empty;
    return name.StartsWith("MyProject") || 
           name.StartsWith("MyCompany");
});
```

### Manual Registration (Selective)

Register a specific endpoint class manually:

```csharp
// Register only ProductEndpoints
app.MapEndpoint<ProductEndpoints>();
```

Use this for hand-written aggregator classes such as `ProductEndpoints`. Do not use it to call generated per-handler `Map{Action}{Entity}` methods; those are already wired through `GeneratedEndpointRoutes.MapGeneratedEndpoints()`.

### What Gets Discovered

Auto-discovery finds classes matching these criteria:

✅ **Included:**
- Class name ends with "Endpoints"
- Is a class (not interface, not abstract)
- Has a public static method: `Map{ClassName}(IEndpointRouteBuilder)`
- Method returns `IEndpointRouteBuilder`
- Not marked with `[ExcludeFromAutoDiscovery]`

Generated endpoint registration surfaces include:
- `GeneratedEndpointRoutes.g.cs` from method-level `[MapPost]`, `[MapGet]`, `[MapPut]`, `[MapPatch]`, and `[MapDelete]` attributes in `BoltHandlerGenerator`. It emits a `MapGeneratedEndpoints()` extension that calls generated per-handler `Map{Action}{Entity}` methods.
- `{Entity}Endpoints.g.cs` from entity-level `[GenerateEndpoints]` in `EntityEndpointGenerator`. These generated endpoint classes are discoverable by `EndpointDiscoveryExtensions.MapGeneratedEndpoints()` because their names end with `Endpoints`.

Application startup should call `app.MapGeneratedEndpoints()` once rather than calling generated `Map{Action}{Entity}` methods directly.

❌ **Excluded:**
- Abstract classes
- Interfaces
- Classes without proper Map method
- Classes marked with `[ExcludeFromAutoDiscovery]`

### Example Discovered Class

```csharp
// This will be auto-discovered
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products");
            
        // Endpoint mappings...
        
        return app;
    }
}
```

## Validator Auto-Detection

`BoltHandlerGenerator` checks whether the compilation contains a concrete validator derived from `AbstractValidator<TRequest>` for the handler's first parameter type. When one exists, the generated REST adapter adds an `IValidator<TRequest>` parameter, runs `ValidateAsync(request, ct)`, and returns `TypedResults.ValidationProblem(...)` before invoking the handler.

Do not add `IValidator<TRequest>` to source-generated `[Map*]` handler signatures. Manual validator injection is reserved for endpoints that are not generated from `[Map*]` attributes, such as endpoints with custom binding or custom `IResult` response logic.

## Service Auto-Discovery

Service discovery is available, but not every module relies on it for every service. Current modules commonly register custom services explicitly and use generated wrappers where cross-module integration needs them. Keep manual registrations when a service has custom lifetime, decorators, conditional setup, or module-specific initialization.

### How It Works

The `AddGeneratedServices()` method:

1. **Scans Assemblies**: Looks through loaded assemblies matching the filter
2. **Finds Service Interfaces**: Identifies interfaces ending with "Service"
3. **Matches Implementations**: Finds corresponding implementation classes
4. **Registers Pairs**: Adds each interface→implementation pair to DI container
5. **Caches Results**: Stores discovered pairs for subsequent calls

### Basic Usage

```csharp
// Default: Scoped lifetime, scans XFramework.* assemblies
builder.Services.AddGeneratedServices();
```

### Custom Lifetime

```csharp
// Use Singleton lifetime
builder.Services.AddGeneratedServices(
    lifetime: ServiceLifetime.Singleton);

// Use Transient lifetime
builder.Services.AddGeneratedServices(
    lifetime: ServiceLifetime.Transient);
```

**Lifetime Guidelines:**
- **Scoped** (default): Use for services with DbContext dependencies
- **Singleton**: Use for stateless, thread-safe services
- **Transient**: Use for lightweight, stateless operations

### Custom Assembly Filter

```csharp
// Only scan specific assemblies
builder.Services.AddGeneratedServices(
    assemblyFilter: asm => asm.FullName?.Contains("Core") == true);
```

### Manual Registration (Selective)

Register a specific service manually:

```csharp
// Register only ProductService
builder.Services.AddService<IProductService, ProductService>();

// With custom lifetime
builder.Services.AddService<IProductService, ProductService>(
    ServiceLifetime.Singleton);
```

### What Gets Discovered

Auto-discovery finds interface/implementation pairs matching these criteria:

✅ **Included:**
- Interface name ends with "Service" (e.g., `IProductService`)
- Implementation name matches interface without 'I' prefix (e.g., `ProductService`)
- Implementation implements the interface
- Implementation is a concrete class (not abstract)
- Neither marked with `[ExcludeFromAutoDiscovery]`

❌ **Excluded:**
- Abstract classes
- Interfaces without matching implementations
- Types marked with `[ExcludeFromAutoDiscovery]`
- Already registered services (skipped)

### Example Discovered Pair

```csharp
// Interface - will be auto-discovered
public interface IProductService
{
    Task<Result<Product>> GetAsync(Guid id);
    Task<Result> CreateAsync(Product product);
}

// Implementation - will be auto-discovered and registered
public class ProductService : IProductService
{
    private readonly DbContext _dbContext;
    
    public ProductService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<Result<Product>> GetAsync(Guid id)
    {
        // Implementation...
    }
    
    public async Task<Result> CreateAsync(Product product)
    {
        // Implementation...
    }
}
```

## Opt-Out Mechanism

### When to Opt-Out

Use `[ExcludeFromAutoDiscovery]` when you need:

- **Custom Registration Logic**: Complex setup, conditional registration
- **Non-Standard Patterns**: Different naming conventions, special cases
- **Manual Control**: Explicit registration order, special configuration
- **Testing Scenarios**: Mock implementations, test doubles

### Excluding Endpoints

```csharp
using XFramework.Core.Attributes;

[ExcludeFromAutoDiscovery("Requires custom authorization setup")]
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Custom endpoint registration with special auth
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Administration");
            
        // ... mappings
        
        return app;
    }
}

// Then manually register in Program.cs:
app.MapEndpoint<AdminEndpoints>();
```

### Excluding Services

```csharp
using XFramework.Core.Attributes;

[ExcludeFromAutoDiscovery("Requires decorator pattern")]
public interface IPaymentService { }

[ExcludeFromAutoDiscovery("Requires decorator pattern")]
public class PaymentService : IPaymentService { }

// Then manually register with decorator in Program.cs:
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.Decorate<IPaymentService, CachedPaymentService>();
```

### Documenting Opt-Out Reasons

```csharp
[ExcludeFromAutoDiscovery(
    Reason = "This service requires manual registration with specific " +
             "configuration and must be registered as Singleton with " +
             "custom initialization parameters.")]
public interface ICustomConfigService { }
```

## Performance Considerations

### Startup Impact

- **First Call**: ~50-100ms (assembly scanning + reflection)
- **Subsequent Calls**: ~1-5ms (cached results)
- **Target**: <100ms startup overhead
- **Measurement**: Logged automatically

### Caching

Discovery results are cached automatically:

```csharp
// First call: Scans assemblies and caches results
app.MapGeneratedEndpoints();

// Subsequent calls: Uses cached results (fast)
app.MapGeneratedEndpoints(); // Uses cache
```

### Clearing Cache (Advanced)

For testing or dynamic assembly loading:

```csharp
using XFramework.Core.Extensions;

// Clear endpoint cache
EndpointDiscoveryExtensions.ClearEndpointCache();

// Clear service cache
ServiceDiscoveryExtensions.ClearServiceCache();
```

### Performance Tips

1. **Use Default Filter**: Default assembly filter is optimized
2. **Avoid Overly Broad Filters**: Narrows scan scope
3. **Monitor Logs**: Check startup time in logs
4. **Cache Warmup**: Discovery happens once at startup

This section describes reflection-discovery caching only. Do not use it as guidance for entity response caching; use [XFramework Caching Strategy](../best-practices/xframework-caching-strategy.md) for cache semantics.

## Troubleshooting

### Endpoints Not Appearing

**Symptom**: Endpoints don't show up in Swagger or aren't accessible

**Solutions**:

1. **Check Method Signature**:
   ```csharp
   // ✅ Correct
   public static IEndpointRouteBuilder MapProductEndpoints(
       this IEndpointRouteBuilder app)
   
   // ❌ Wrong return type
   public static void MapProductEndpoints(IEndpointRouteBuilder app)
   
   // ❌ Wrong parameter type
   public static IEndpointRouteBuilder MapProductEndpoints(
       WebApplication app)
   ```

2. **Check Naming Convention**:
   ```csharp
   // ✅ Correct
   public static class ProductEndpoints
   {
       public static IEndpointRouteBuilder MapProductEndpoints(...) 
   }
   
   // ❌ Name doesn't match class name
   public static class ProductEndpoints
   {
       public static IEndpointRouteBuilder MapProducts(...) 
   }
   ```

3. **Check for Exclusion Attribute**:
   ```csharp
   // Remove if unintended:
   [ExcludeFromAutoDiscovery] // <-- Remove this
   public static class ProductEndpoints
   ```

4. **Check Assembly Filter**:
   ```csharp
   // Ensure your assembly matches the filter
   app.MapGeneratedEndpoints(asm => 
   {
       Console.WriteLine($"Checking: {asm.FullName}");
       return asm.FullName?.StartsWith("XFramework") == true;
   });
   ```

5. **Check Logs**:
   ```
   // Look for these log messages:
   [INFO] Starting auto-discovery of endpoints. Found X endpoint types
   [INFO] Successfully registered endpoints from ProductEndpoints
   [INFO] Endpoint auto-discovery completed in Xms
   ```

### Services Not Injected

**Symptom**: `InvalidOperationException: Unable to resolve service for type 'IProductService'`

**Solutions**:

1. **Check Naming Convention**:
   ```csharp
   // ✅ Correct
   public interface IProductService { }
   public class ProductService : IProductService { }
   
   // ❌ Name mismatch
   public interface IProductService { }
   public class ProductServiceImpl : IProductService { } // Won't match
   ```

2. **Check Interface Implementation**:
   ```csharp
   // ✅ Correct
   public class ProductService : IProductService
   
   // ❌ Doesn't implement interface
   public class ProductService // Won't be registered
   ```

3. **Check for Exclusion Attribute**: Remove if unintended

4. **Check Assembly**: Ensure service assembly is loaded and matches filter

5. **Check Registration**:
   ```csharp
   // Verify AddGeneratedServices() is called BEFORE Build()
   builder.Services.AddGeneratedServices(); // ✅ Before Build()
   var app = builder.Build();
   ```

### Duplicate Registration Warnings

**Symptom**: `Service IProductService already registered, skipping`

**Cause**: Service manually registered AND auto-discovered

**Solutions**:

1. **Remove Manual Registration**: Let auto-discovery handle it
2. **Or Exclude from Auto-Discovery**: Use `[ExcludeFromAutoDiscovery]`
3. **Or Register After**: Manual registrations after auto-discovery override

### Performance Issues

**Symptom**: Slow startup time

**Solutions**:

1. **Check Filter Scope**: Ensure filter isn't too broad
2. **Check Assembly Count**: Narrow filter to needed assemblies
3. **Check Logs**: Look for assembly loading errors
4. **Profile**: Use diagnostic tools to identify bottleneck

## Advanced Usage

### Custom Discovery Patterns

For non-standard patterns, create wrapper methods:

```csharp
public static class CustomDiscovery
{
    public static IServiceCollection AddProjectServices(
        this IServiceCollection services)
    {
        // Add generated services
        services.AddGeneratedServices();
        
        // Add custom services
        services.AddScoped<ICustomService, CustomService>();
        
        // Add decorated services
        services.AddScoped<IPaymentService, PaymentService>();
        services.Decorate<IPaymentService, CachedPaymentService>();
        
        return services;
    }
    
    public static IEndpointRouteBuilder MapProjectEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Map generated endpoints
        app.MapGeneratedEndpoints();
        
        // Map custom endpoints
        app.MapEndpoint<AdminEndpoints>();
        
        return app;
    }
}

// Usage in Program.cs:
builder.Services.AddProjectServices();
app.MapProjectEndpoints();
```

### Conditional Registration

```csharp
// Register based on configuration
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddGeneratedServices(
        asm => asm.FullName?.Contains("Dev") == true);
}
else
{
    builder.Services.AddGeneratedServices(
        asm => asm.FullName?.StartsWith("XFramework") == true);
}
```

### Module-Based Discovery

```csharp
public static class InventarioModule
{
    public static IServiceCollection AddInventarioServices(
        this IServiceCollection services)
    {
        return services.AddGeneratedServices(
            asm => asm.FullName?.Contains("Inventario") == true);
    }
    
    public static IEndpointRouteBuilder MapInventarioEndpoints(
        this IEndpointRouteBuilder app)
    {
        return app.MapGeneratedEndpoints(
            asm => asm.FullName?.Contains("Inventario") == true);
    }
}

// Usage:
builder.Services.AddInventarioServices();
app.MapInventarioEndpoints();
```

### Integration with Existing Code

```csharp
// Gradual migration approach
var app = builder.Build();

// Keep existing manual registrations during migration
app.MapProductEndpoints(); // Old way
app.MapOrderEndpoints();   // Old way

// Add auto-discovery for generated endpoints
app.MapGeneratedEndpoints(); // Current generated registration

app.Run();
```

## Next Steps

- [Migration Guide](../developer-experience/migration-to-auto-discovery.md) - Migrating existing code
- [Attribute Usage Guide](./generate-endpoints-attribute-usage.md) - Using `[GenerateEndpoints]`
- [Troubleshooting](#troubleshooting) - Common issues and solutions

---

**Version**: 1.0
**Last Updated**: 2026-05-21
**Phase**: 5.4 - Auto-Discovery & Registration

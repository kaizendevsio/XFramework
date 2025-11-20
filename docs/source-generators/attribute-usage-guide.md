# Attribute Usage Guide - GenerateEndpoints

This guide documents the usage of the `GenerateEndpointsAttribute` and related types for entity-centric code generation in XFramework.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Attribute Properties](#attribute-properties)
- [Usage Examples](#usage-examples)
- [Design Decisions](#design-decisions)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## Overview

The `GenerateEndpointsAttribute` enables automatic code generation for entity CRUD operations, reducing boilerplate code while maintaining flexibility and control. When applied to an entity class, the source generator can create:

- **Service Layer**: Business logic layer with standard CRUD operations
- **REST Endpoints**: Minimal API endpoints following VSA (Vertical Slice Architecture) patterns
- **Both**: Complete implementation with service and endpoints (recommended)

### Key Benefits

1. **Reduced Boilerplate**: Eliminate repetitive CRUD code
2. **Consistency**: Ensures all entities follow the same patterns
3. **Flexibility**: Fine-grained control over what gets generated
4. **Type Safety**: Compile-time validation of configurations
5. **Maintainability**: Changes to patterns are applied automatically

## Quick Start

### Basic Example

```csharp
using XFramework.Core.Attributes;
using XFramework.Domain.Entities;

[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/products"
)]
public partial class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}
```

**What gets generated:**
- `IProductService` interface with CRUD methods
- `ProductService` implementation
- Minimal API endpoints: POST, GET, GET (list), PUT, DELETE at `/api/products`
- Automatic caching for GET operations (5 minutes default)
- Authorization required by default

## Attribute Properties

### Type (EndpointType)

Controls what gets generated for the entity.

```csharp
public EndpointType Type { get; set; } = EndpointType.Both;
```

| Value | Description | Use When |
|-------|-------------|----------|
| `Service` | Generate service layer only | You need custom endpoints but want standard service layer |
| `Rest` | Generate REST endpoints only | Business logic exists elsewhere or is very simple |
| `Both` | Generate both service and endpoints | Standard CRUD with VSA pattern (recommended) |

### Actions (EndpointActions)

Specifies which CRUD operations to generate (flags enum).

```csharp
public EndpointActions Actions { get; set; } = EndpointActions.All;
```

| Flag | HTTP Method | Description |
|------|-------------|-------------|
| `Create` | POST | Create new entity |
| `Get` | GET /{id} | Get single entity by ID |
| `GetList` | GET / | Get paginated list of entities |
| `Update` | PUT /{id} | Update existing entity |
| `Delete` | DELETE /{id} | Delete entity by ID |

**Convenience Combinations:**

| Combination | Equivalent To | Use When |
|-------------|---------------|----------|
| `All` | Create \| Get \| GetList \| Update \| Delete | Full CRUD functionality needed |
| `ReadOnly` | Get \| GetList | Lookup/reference data |
| `WriteOnly` | Create \| Update \| Delete | Separate read paths exist |
| `Standard` | Create \| Get \| GetList \| Update | Soft delete or no delete needed |

### RoutePrefix (string?)

Base route path for generated endpoints.

```csharp
public string? RoutePrefix { get; set; }
```

- **Default**: Uses entity name in lowercase (e.g., `"api/products"` for `Product`)
- **Format**: Should follow REST conventions, typically `"api/{resource}"`
- **Versioning**: Can include version (e.g., `"api/v2/products"`)
- **Nesting**: Supports nested resources (e.g., `"api/categories/{categoryId}/products"`)

### RequireAuthorization (bool)

Controls whether endpoints require authentication.

```csharp
public bool RequireAuthorization { get; set; } = true;
```

- **Default**: `true` (secure by default)
- **When true**: Applies `[Authorize]` attribute to endpoints
- **When false**: Allows anonymous access (use carefully!)

⚠️ **Security Note**: Defaults to `true` for security. Only set to `false` for truly public data.

### Roles (string[]?)

Specifies required roles for endpoint access.

```csharp
public string[]? Roles { get; set; }
```

- **Default**: `null` (any authenticated user)
- **When set**: User must have at least one of the specified roles
- **Only effective** when `RequireAuthorization = true`

### CacheDurationSeconds (int)

Cache duration for GET operations in seconds.

```csharp
public int CacheDurationSeconds { get; set; } = 300;
```

- **Default**: 300 seconds (5 minutes)
- **Range**: 0 to unlimited (0 disables caching)
- **Applies to**: GET and GetList operations only
- **Invalidation**: Automatic on Create/Update/Delete

**Common Values:**
- `0`: No caching (frequently changing data)
- `300`: 5 minutes (default, moderate volatility)
- `600`: 10 minutes (stable data)
- `3600`: 1 hour (reference/lookup data)

### CacheKeyPrefix (string?)

Prefix for cache key generation.

```csharp
public string? CacheKeyPrefix { get; set; }
```

- **Default**: `null` (uses entity name in lowercase)
- **Format**: `{CacheKeyPrefix}:{EntityId}` for Get, `{CacheKeyPrefix}:list:{QueryHash}` for GetList
- **Use**: Module scoping, multi-tenant scenarios

## Usage Examples

### Example 1: Full CRUD with Security

Standard product management with role-based access control.

```csharp
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.All,
    RoutePrefix = "api/products",
    RequireAuthorization = true,
    Roles = new[] { "Admin", "ProductManager" },
    CacheDurationSeconds = 600,
    CacheKeyPrefix = "products"
)]
public partial class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public Guid CategoryId { get; set; }
}
```

**Generated Endpoints:**
- `POST /api/products` - Create (Admin, ProductManager)
- `GET /api/products/{id}` - Get (Admin, ProductManager, cached 10 min)
- `GET /api/products` - GetList (Admin, ProductManager, cached 10 min)
- `PUT /api/products/{id}` - Update (Admin, ProductManager)
- `DELETE /api/products/{id}` - Delete (Admin, ProductManager)

### Example 2: Read-Only Lookup Data

Public reference data with aggressive caching.

```csharp
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.ReadOnly,
    RoutePrefix = "api/lookup/categories",
    RequireAuthorization = false,
    CacheDurationSeconds = 3600,
    CacheKeyPrefix = "lookup:categories"
)]
public partial class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
```

**Generated Endpoints:**
- `GET /api/lookup/categories/{id}` - Get (public, cached 1 hour)
- `GET /api/lookup/categories` - GetList (public, cached 1 hour)

**Use Case**: Reference data, lookup tables, categories, countries, etc.

### Example 3: Service Only (Custom Endpoints)

Generate service layer but handle endpoints manually for complex routing.

```csharp
[GenerateEndpoints(
    Type = EndpointType.Service,
    Actions = EndpointActions.All
)]
public partial class Order : BaseEntity
{
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}
```

**Generated:**
- `IOrderService` interface
- `OrderService` implementation
- No endpoints (you create custom ones with specific business logic)

**Manual Endpoint Example:**
```csharp
// Custom endpoint with complex validation and workflow
public class CreateOrderEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders", async (
            CreateOrderRequest request,
            IOrderService orderService,
            IInventoryService inventoryService) =>
        {
            // Complex validation
            await inventoryService.ValidateStock(request.Items);
            
            // Use generated service
            var order = await orderService.CreateAsync(request);
            
            // Additional workflow
            await orderService.SendConfirmationEmail(order.Id);
            
            return Results.Created($"/api/orders/{order.Id}", order);
        })
        .RequireAuthorization()
        .WithTags("Orders");
    }
}
```

### Example 4: Write Operations Only

Separate read and write paths for performance.

```csharp
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.WriteOnly,
    RoutePrefix = "api/audit-logs",
    RequireAuthorization = true,
    Roles = new[] { "Admin", "Auditor" },
    CacheDurationSeconds = 0 // No caching for audit logs
)]
public partial class AuditLog : BaseEntity
{
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Details { get; set; } = string.Empty;
}
```

**Generated Endpoints:**
- `POST /api/audit-logs` - Create
- `PUT /api/audit-logs/{id}` - Update
- `DELETE /api/audit-logs/{id}` - Delete

**Use Case**: Read operations handled by separate reporting/analytics system.

### Example 5: No Delete (Soft Delete Pattern)

Standard CRUD excluding hard delete.

```csharp
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Standard, // Excludes Delete
    RoutePrefix = "api/customers",
    RequireAuthorization = true,
    CacheDurationSeconds = 300
)]
public partial class Customer : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } // Soft delete flag
    public DateTime? DeletedAt { get; set; }
}
```

**Pattern**: Implement soft delete in service layer; no hard delete endpoint generated.

### Example 6: Selective Operations

Generate only specific operations needed.

```csharp
[GenerateEndpoints(
    Type = EndpointType.Both,
    Actions = EndpointActions.Create | EndpointActions.Get | EndpointActions.GetList,
    RoutePrefix = "api/notifications",
    RequireAuthorization = true,
    CacheDurationSeconds = 60 // Short cache for notifications
)]
public partial class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Generated:**
- Create notification
- Get notification
- List notifications
- No Update or Delete (handled via separate mark-as-read endpoint)

## Design Decisions

### 1. Why Opt-In via Attribute?

**Decision**: Entities must explicitly use `[GenerateEndpoints]` for code generation.

**Rationale**:
- Prevents accidental exposure of internal entities
- Clear intent and discoverability
- Allows gradual migration
- Better control over what's generated

### 2. Why Flags Enum for Actions?

**Decision**: Use `[Flags]` enum for `EndpointActions` instead of individual boolean properties.

**Rationale**:
- Combines multiple operations concisely: `Create | Get | Update`
- Provides convenience combinations: `All`, `ReadOnly`, `Standard`
- More maintainable than 5+ boolean properties
- Common pattern in .NET (e.g., `FileAccess`)

### 3. Why Secure Defaults?

**Decision**: Default `RequireAuthorization = true` and `CacheDurationSeconds = 300`.

**Rationale**:
- Security by default principle
- Prevents accidental data exposure
- Reasonable cache duration for most scenarios
- Explicit opt-out required for public endpoints

### 4. Why Separate Type and Actions?

**Decision**: Separate `Type` (what to generate) from `Actions` (which operations).

**Rationale**:
- Orthogonal concerns: generation scope vs operation selection
- Allows "service only with specific actions"
- More flexible than combined approach
- Clearer intent

### 5. Why RoutePrefix Optional?

**Decision**: `RoutePrefix` defaults to entity name if not specified.

**Rationale**:
- Convention over configuration
- Reduces verbosity for standard cases
- Still allows customization for complex routing
- Follows REST conventions automatically

## Best Practices

### 1. Entity Design

```csharp
// ✅ GOOD: Partial class for extensibility
[GenerateEndpoints(...)]
public partial class Product : BaseEntity
{
    // Generator adds service/endpoint logic in separate file
}

// ❌ BAD: Non-partial class
[GenerateEndpoints(...)]
public class Product : BaseEntity // Can't extend in generated code
{
}
```

### 2. Authorization Strategy

```csharp
// ✅ GOOD: Use roles for different access levels
[GenerateEndpoints(
    RequireAuthorization = true,
    Roles = new[] { "Admin", "ProductManager" }
)]

// ⚠️ CAREFUL: Public endpoints for truly public data only
[GenerateEndpoints(
    RequireAuthorization = false, // Only for public lookup data
    Actions = EndpointActions.ReadOnly // Never allow writes without auth
)]

// ❌ BAD: Public write endpoints
[GenerateEndpoints(
    RequireAuthorization = false,
    Actions = EndpointActions.All // Security risk!
)]
```

### 3. Caching Strategy

```csharp
// ✅ GOOD: Match cache duration to data volatility
[GenerateEndpoints(
    CacheDurationSeconds = 3600  // Reference data: 1 hour
)]
public partial class Country : BaseEntity { }

[GenerateEndpoints(
    CacheDurationSeconds = 300   // Normal data: 5 minutes
)]
public partial class Product : BaseEntity { }

[GenerateEndpoints(
    CacheDurationSeconds = 0     // Real-time data: no cache
)]
public partial class StockPrice : BaseEntity { }
```

### 4. Action Selection

```csharp
// ✅ GOOD: Use convenience combinations
Actions = EndpointActions.ReadOnly  // Clear intent

// ✅ GOOD: Combine specific operations
Actions = EndpointActions.Create | EndpointActions.Get

// ❌ BAD: Redundant specification
Actions = EndpointActions.All & ~EndpointActions.Delete  // Use Standard instead
```

### 5. Route Design

```csharp
// ✅ GOOD: Follow REST conventions
RoutePrefix = "api/products"
RoutePrefix = "api/v2/products"  // Versioning
RoutePrefix = "api/categories/{categoryId}/products"  // Nested

// ❌ BAD: Inconsistent or unclear routing
RoutePrefix = "/products"  // Missing api prefix
RoutePrefix = "GetProducts"  // Not RESTful
```

## Troubleshooting

### Issue: Attribute Not Recognized

**Symptom**: Attribute shows as undefined or generator doesn't run.

**Solution**:
```csharp
// Add using statement
using XFramework.Core.Attributes;

// Ensure XFramework.Core is referenced
<ProjectReference Include="..\..\Kernel\XFramework.Core\XFramework.Core.csproj" />
```

### Issue: Compilation Errors in Generated Code

**Symptom**: Errors in generated files (*.g.cs).

**Solution**:
1. Ensure entity inherits from `BaseEntity`
2. Make entity class `partial`
3. Check that properties are publicly accessible
4. Rebuild the project completely

### Issue: Endpoints Not Appearing

**Symptom**: Endpoints aren't registered at runtime.

**Solution**:
```csharp
// Ensure generated endpoints are mapped in Program.cs
app.MapGeneratedEndpoints();  // Or equivalent registration call
```

### Issue: Cache Not Working

**Symptom**: Data not cached despite `CacheDurationSeconds > 0`.

**Solution**:
1. Verify caching is configured in dependency injection
2. Check Redis connection if using distributed cache
3. Ensure cache key prefix doesn't conflict
4. Verify no explicit cache bypass in requests

### Issue: Authorization Always Fails

**Symptom**: All requests return 401 Unauthorized.

**Solution**:
1. Verify JWT/Auth middleware is configured
2. Check token is included in request headers
3. Verify user has required roles (if `Roles` specified)
4. Check authentication scheme matches configuration

## Auto-Discovery Integration

### The Complete Pipeline

The `[GenerateEndpoints]` attribute is the starting point of a fully automated pipeline:

1. **Attribute Application** → You apply `[GenerateEndpoints]` to entity
2. **Source Generation** → Generators create service and endpoint code
3. **Auto-Discovery** → Extensions automatically register everything
4. **Runtime** → Application runs with zero manual configuration

### Auto-Discovery Behavior

When you apply `[GenerateEndpoints]`, the auto-discovery system will:

✅ **Automatically Find**: Generated endpoint classes ending with "Endpoints"
✅ **Automatically Map**: Invoke `Map*Endpoints()` methods
✅ **Automatically Register**: Service interface/implementation pairs
✅ **Zero Configuration**: Works immediately without manual registration

### Using Auto-Discovery

Instead of manual registration:

```csharp
// ❌ OLD WAY: Manual registration required
app.MapProductEndpoints();
app.MapOrderEndpoints();
// ... one call per entity
```

Use auto-discovery in `Program.cs`:

```csharp
// ✅ NEW WAY: Single call discovers all
using XFramework.Core.Extensions;

// Auto-register all generated services
builder.Services.AddGeneratedServices();

// Auto-map all generated endpoints
app.MapGeneratedEndpoints();
```

### Complete Example with Auto-Discovery

```csharp
// 1. Apply attribute to entity
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

// 2. Update Program.cs ONCE (works for all entities)
using XFramework.Core.Extensions;

var builder = XApplication.Configure<Program>();

// Auto-discover services
builder.Services.AddGeneratedServices();

var app = (WebApplication)builder.Build();

// Auto-discover endpoints
app.MapGeneratedEndpoints();

app.Run();

// 3. That's it! No manual registration needed.
//    When you add a new entity with [GenerateEndpoints],
//    it automatically appears without touching Program.cs.
```

### Opt-Out When Needed

For special cases requiring manual control, exclude from auto-discovery:

```csharp
using XFramework.Core.Attributes;

[GenerateEndpoints(...)]
[ExcludeFromAutoDiscovery("Requires custom authorization setup")]
public partial class AdminEntity : BaseEntity
{
    // This will be generated but NOT auto-discovered
    // You must manually register it
}

// Then in Program.cs:
app.MapGeneratedEndpoints(); // Discovers all others
app.MapEndpoint<AdminEntityEndpoints>(); // Manual for excluded
```

## Next Steps

After defining attributes:

1. **✅ Phase 5.2**: `EntityServiceGenerator` - Completed
2. **✅ Phase 5.3**: `EntityEndpointGenerator` - Completed
3. **✅ Phase 5.4**: Auto-Discovery & Registration - Completed
4. **Phase 5.5**: Testing - Add integration tests for generated code
5. **Phase 5.6**: Migration - Apply to all existing entities

**Ready to use the full pipeline!** See:
- [Auto-Discovery Guide](./auto-discovery-guide.md) - Complete usage documentation
- [Migration Guide](./migration-to-auto-discovery.md) - Migrate existing projects

## Related Documentation

- [Auto-Discovery Guide](./auto-discovery-guide.md) - Automatic registration system
- [Migration Guide](./migration-to-auto-discovery.md) - Migrating to auto-discovery
- [VSA Architecture Guide](../architecture/vsa-guide.md)
- [Source Generators Overview](../source-generators/overview.md)
- [Caching Strategy](../caching-strategy.md)
- [Security Best Practices](../security/best-practices.md)

---

**Version**: 1.1
**Last Updated**: 2025-11-20
**Phase**: 5.4 - Auto-Discovery & Registration (Updated)
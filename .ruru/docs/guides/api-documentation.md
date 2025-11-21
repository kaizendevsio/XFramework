# API Documentation Guide - XFramework

## Overview

Well-documented APIs are crucial for developer experience and maintainability. XFramework uses Swagger/OpenAPI for interactive API documentation, enhanced with XML comments and structured metadata. This guide covers best practices for documenting your APIs.

## Table of Contents

1. [Swagger Configuration](#swagger-configuration)
2. [XML Documentation Standards](#xml-documentation-standards)
3. [Adding Operation Examples](#adding-operation-examples)
4. [Authentication Documentation](#authentication-documentation)
5. [API Versioning Strategy](#api-versioning-strategy)
6. [Response Type Documentation](#response-type-documentation)
7. [Error Response Documentation](#error-response-documentation)

---

## Swagger Configuration

### Basic Setup

XFramework's Swagger is configured in `Program.cs`:

```csharp
// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "XFramework API",
        Version = "v1",
        Description = "XFramework REST API for managing business operations",
        Contact = new OpenApiContact
        {
            Name = "Development Team",
            Email = "dev@xframework.com",
            Url = new Uri("https://xframework.com")
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);

    // Add JWT authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Enable Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "XFramework API V1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
        options.DocumentTitle = "XFramework API Documentation";
        options.DisplayRequestDuration();
    });
}
```

### Enable XML Documentation

Update your `.csproj` file:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn> <!-- Suppress missing XML comment warnings -->
</PropertyGroup>
```

---

## XML Documentation Standards

### Documenting Endpoints

**Minimal API Format:**

```csharp
/// <summary>
/// Creates a new product in the catalog
/// </summary>
/// <remarks>
/// Sample request:
/// 
///     POST /api/products
///     {
///        "name": "Gaming Laptop",
///        "sku": "LAPTOP-001",
///        "price": 1299.99,
///        "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
///     }
/// 
/// </remarks>
/// <param name="service">Product service for business logic</param>
/// <param name="request">Product creation request</param>
/// <returns>The created product with generated ID</returns>
/// <response code="201">Product created successfully</response>
/// <response code="400">Invalid request data</response>
/// <response code="401">Unauthorized - Authentication required</response>
/// <response code="500">Internal server error</response>
app.MapPost("/api/products", async (
    IProductService service,
    [FromBody] CreateProductRequest request) =>
{
    var result = await service.CreateAsync(request);
    return result.IsSuccess
        ? Results.Created($"/api/products/{result.Data.Id}", result.Data)
        : Results.BadRequest(new { error = result.Message });
})
.WithName("CreateProduct")
.WithTags("Products")
.WithOpenApi()
.Produces<Product>(201)
.ProducesProblem(400)
.ProducesProblem(401)
.ProducesProblem(500);
```

### Documenting Request Models

```csharp
/// <summary>
/// Request model for creating a new product
/// </summary>
public record CreateProductRequest
{
    /// <summary>
    /// Product name (required, 3-100 characters)
    /// </summary>
    /// <example>Gaming Laptop</example>
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Stock Keeping Unit - unique product identifier
    /// </summary>
    /// <example>LAPTOP-001</example>
    [Required]
    [StringLength(50)]
    public string SKU { get; init; } = string.Empty;

    /// <summary>
    /// Product price in USD
    /// </summary>
    /// <example>1299.99</example>
    [Required]
    [Range(0.01, 999999.99)]
    public decimal Price { get; init; }

    /// <summary>
    /// Category identifier (must exist)
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    [Required]
    public Guid CategoryId { get; init; }

    /// <summary>
    /// Optional product description
    /// </summary>
    /// <example>High-performance gaming laptop with RTX 4080 GPU</example>
    [StringLength(1000)]
    public string? Description { get; init; }
}
```

### Documenting Response Models

```csharp
/// <summary>
/// Product entity representing a catalog item
/// </summary>
public class Product
{
    /// <summary>
    /// Unique product identifier
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    public Guid Id { get; set; }

    /// <summary>
    /// Product name
    /// </summary>
    /// <example>Gaming Laptop</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stock Keeping Unit
    /// </summary>
    /// <example>LAPTOP-001</example>
    public string SKU { get; set; } = string.Empty;

    /// <summary>
    /// Current price in USD
    /// </summary>
    /// <example>1299.99</example>
    public decimal Price { get; set; }

    /// <summary>
    /// Product category identifier
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    public Guid CategoryId { get; set; }

    /// <summary>
    /// When the product was created (UTC)
    /// </summary>
    /// <example>2025-11-20T10:30:00Z</example>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Whether the product is currently active
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; set; }
}
```

### Documenting Service Methods

```csharp
/// <summary>
/// Service for managing product catalog operations
/// </summary>
public class ProductService : IProductService
{
    /// <summary>
    /// Creates a new product in the catalog
    /// </summary>
    /// <param name="request">Product creation details</param>
    /// <param name="ct">Cancellation token for async operation</param>
    /// <returns>
    /// A Result containing the created product with generated ID and timestamps,
    /// or a failure result with error details
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
    /// <remarks>
    /// This method validates the request, checks for duplicate SKUs,
    /// creates the product entity, and invalidates related caches.
    /// </remarks>
    public async Task<Result<Product>> CreateAsync(
        CreateProductRequest request,
        CancellationToken ct = default)
    {
        // Implementation
    }
}
```

---

## Adding Operation Examples

### Using WithOpenApi() Extension

```csharp
app.MapPost("/api/products", async (
    IProductService service,
    CreateProductRequest request) =>
{
    var result = await service.CreateAsync(request);
    return result.IsSuccess
        ? Results.Created($"/api/products/{result.Data.Id}", result.Data)
        : Results.BadRequest(new { error = result.Message });
})
.WithOpenApi(operation =>
{
    operation.Summary = "Create a new product";
    operation.Description = "Creates a new product in the catalog with validation";
    
    // Add request example
    operation.RequestBody.Content["application/json"].Example = new OpenApiExample
    {
        Summary = "Standard Product",
        Value = new CreateProductRequest
        {
            Name = "Gaming Laptop",
            SKU = "LAPTOP-001",
            Price = 1299.99m,
            CategoryId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            Description = "High-performance gaming laptop"
        }
    };

    return operation;
});
```

### Multiple Examples

```csharp
.WithOpenApi(operation =>
{
    var examples = new OpenApiObject
    {
        ["standardProduct"] = new OpenApiObject
        {
            ["summary"] = new OpenApiString("Standard Product"),
            ["value"] = new OpenApiObject
            {
                ["name"] = new OpenApiString("Gaming Laptop"),
                ["sku"] = new OpenApiString("LAPTOP-001"),
                ["price"] = new OpenApiDouble(1299.99),
                ["categoryId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6")
            }
        },
        ["budgetProduct"] = new OpenApiObject
        {
            ["summary"] = new OpenApiString("Budget Product"),
            ["value"] = new OpenApiObject
            {
                ["name"] = new OpenApiString("Office Mouse"),
                ["sku"] = new OpenApiString("MOUSE-001"),
                ["price"] = new OpenApiDouble(9.99),
                ["categoryId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6")
            }
        }
    };

    operation.RequestBody.Content["application/json"].Examples = examples;
    return operation;
});
```

---

## Authentication Documentation

### JWT Bearer Authentication

Already configured in Swagger setup (see [Swagger Configuration](#swagger-configuration)).

### Documenting Protected Endpoints

```csharp
/// <summary>
/// Retrieves all products for the authenticated user's tenant
/// </summary>
/// <remarks>
/// Requires valid JWT token in Authorization header.
/// Only returns products belonging to the user's tenant.
/// </remarks>
app.MapGet("/api/products", async (
    IProductService service,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20) =>
{
    var request = new GetProductListRequest { Page = page, PageSize = pageSize };
    var result = await service.GetListAsync(request);
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.BadRequest(new { error = result.Message });
})
.WithName("GetProducts")
.WithTags("Products")
.RequireAuthorization() // Adds lock icon in Swagger UI
.Produces<List<Product>>(200)
.ProducesProblem(401)
.ProducesProblem(403);
```

### Documenting Role-Based Access

```csharp
/// <summary>
/// Deletes a product (Admin only)
/// </summary>
/// <remarks>
/// Requires 'Admin' role.
/// Performs soft delete - sets IsDeleted flag to true.
/// </remarks>
app.MapDelete("/api/products/{id:guid}", async (
    Guid id,
    IProductService service) =>
{
    var result = await service.DeleteAsync(id);
    return result.IsSuccess
        ? Results.NoContent()
        : Results.NotFound(new { error = result.Message });
})
.WithName("DeleteProduct")
.WithTags("Products")
.RequireAuthorization("AdminPolicy") // Specific policy
.Produces(204)
.ProducesProblem(401)
.ProducesProblem(403)
.ProducesProblem(404);
```

---

## API Versioning Strategy

### URL Path Versioning (Recommended)

```csharp
// Configure versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Version 1 endpoints
var v1 = app.MapGroup("/api/v1")
    .WithTags("V1");

v1.MapGet("/products", async (IProductService service) =>
{
    // V1 implementation
})
.WithName("GetProducts_V1");

// Version 2 endpoints with new features
var v2 = app.MapGroup("/api/v2")
    .WithTags("V2");

v2.MapGet("/products", async (IProductServiceV2 service) =>
{
    // V2 implementation with additional fields
})
.WithName("GetProducts_V2");
```

### Swagger Multi-Version Support

```csharp
builder.Services.AddSwaggerGen(options =>
{
    // V1 Documentation
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "XFramework API V1",
        Version = "v1",
        Description = "Version 1 - Stable API"
    });

    // V2 Documentation
    options.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "XFramework API V2",
        Version = "v2",
        Description = "Version 2 - Enhanced features (Beta)"
    });

    options.DocInclusionPredicate((docName, apiDesc) =>
    {
        if (!apiDesc.TryGetMethodInfo(out var methodInfo)) return false;
        
        var versions = methodInfo.DeclaringType?
            .GetCustomAttributes(true)
            .OfType<ApiVersionAttribute>()
            .SelectMany(attr => attr.Versions);

        return versions?.Any(v => $"v{v}" == docName) ?? false;
    });
});

// UI configuration
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "V1");
    options.SwaggerEndpoint("/swagger/v2/swagger.json", "V2");
});
```

### Deprecation Notices

```csharp
/// <summary>
/// Legacy endpoint for product creation
/// </summary>
/// <remarks>
/// ⚠️ **DEPRECATED**: This endpoint is deprecated and will be removed in V3.
/// Please use POST /api/v2/products instead.
/// 
/// Reason for deprecation: Missing required validation fields.
/// Removal date: 2026-01-01
/// </remarks>
[Obsolete("Use V2 endpoint. Will be removed in V3.")]
app.MapPost("/api/v1/products/legacy", async (...) =>
{
    // Old implementation
})
.WithName("CreateProduct_Legacy")
.WithTags("V1", "Deprecated");
```

---

## Response Type Documentation

### Success Responses

```csharp
app.MapGet("/api/products/{id:guid}", async (Guid id, IProductService service) =>
{
    var result = await service.GetByIdAsync(id);
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.NotFound(new { error = result.Message });
})
.WithName("GetProduct")
.Produces<Product>(200, "application/json")
.ProducesProblem(404)
.ProducesValidationProblem(400);
```

### Paginated Responses

```csharp
/// <summary>
/// Paginated response wrapper
/// </summary>
/// <typeparam name="T">Item type</typeparam>
public record PagedResponse<T>
{
    /// <summary>
    /// Items for current page
    /// </summary>
    public List<T> Items { get; init; } = new();

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    /// <example>1</example>
    public int Page { get; init; }

    /// <summary>
    /// Items per page
    /// </summary>
    /// <example>20</example>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    /// <example>157</example>
    public int TotalCount { get; init; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    /// <example>8</example>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>
    /// Whether there is a next page
    /// </summary>
    /// <example>true</example>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    /// <example>false</example>
    public bool HasPreviousPage => Page > 1;
}

app.MapGet("/api/products", async (
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    IProductService service) =>
{
    // Implementation
})
.Produces<PagedResponse<Product>>(200);
```

---

## Error Response Documentation

### Standard Error Response

```csharp
/// <summary>
/// Standard error response
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// Error message
    /// </summary>
    /// <example>Product not found</example>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// HTTP status code
    /// </summary>
    /// <example>404</example>
    public int StatusCode { get; init; }

    /// <summary>
    /// Timestamp when error occurred (UTC)
    /// </summary>
    /// <example>2025-11-20T10:30:00Z</example>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Request correlation ID for tracing
    /// </summary>
    /// <example>a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d</example>
    public string? CorrelationId { get; init; }
}
```

### Validation Error Response

```csharp
/// <summary>
/// Validation error response with field-level errors
/// </summary>
public record ValidationErrorResponse
{
    /// <summary>
    /// Overall validation error message
    /// </summary>
    /// <example>Validation failed</example>
    public string Error { get; init; } = "Validation failed";

    /// <summary>
    /// Field-specific validation errors
    /// </summary>
    public Dictionary<string, string[]> Errors { get; init; } = new();

    /// <summary>
    /// Example response
    /// </summary>
    public static ValidationErrorResponse Example => new()
    {
        Error = "Validation failed",
        Errors = new Dictionary<string, string[]>
        {
            ["Name"] = new[] { "Name is required", "Name must be at least 3 characters" },
            ["Price"] = new[] { "Price must be greater than 0" }
        }
    };
}
```

### Documenting All Error Codes

```csharp
/// <summary>
/// Creates a new product
/// </summary>
/// <response code="201">Product created successfully</response>
/// <response code="400">
/// Validation failed. Check the errors dictionary for field-specific issues.
/// Possible validation errors:
/// - Name: Required, length 3-100
/// - SKU: Required, must be unique
/// - Price: Must be positive
/// </response>
/// <response code="401">Unauthorized - Missing or invalid JWT token</response>
/// <response code="403">Forbidden - User lacks required permissions</response>
/// <response code="409">Conflict - Product with this SKU already exists</response>
/// <response code="500">Internal server error - Check logs for details</response>
app.MapPost("/api/products", async (...) =>
{
    // Implementation
})
.Produces<Product>(201)
.ProducesValidationProblem(400)
.ProducesProblem(401)
.ProducesProblem(403)
.ProducesProblem(409)
.ProducesProblem(500);
```

---

## Complete Example: Fully Documented Endpoint

```csharp
/// <summary>
/// Updates an existing product
/// </summary>
/// <remarks>
/// Updates product information. Only provided fields will be updated (PATCH semantics).
/// Requires authentication and appropriate permissions.
/// 
/// Sample request:
/// 
///     PUT /api/products/3fa85f64-5717-4562-b3fc-2c963f66afa6
///     {
///        "name": "Updated Gaming Laptop",
///        "price": 1199.99
///     }
/// 
/// Changes are immediately reflected in the API but may take up to 5 minutes
/// to propagate to all caches.
/// </remarks>
/// <param name="id">Product identifier</param>
/// <param name="request">Fields to update</param>
/// <param name="service">Product service</param>
/// <response code="200">Product updated successfully</response>
/// <response code="400">Invalid request data</response>
/// <response code="401">Unauthorized - Authentication required</response>
/// <response code="403">Forbidden - Insufficient permissions</response>
/// <response code="404">Product not found</response>
/// <response code="409">Conflict - Concurrent update detected</response>
/// <response code="500">Internal server error</response>
app.MapPut("/api/products/{id:guid}", async (
    Guid id,
    [FromBody] UpdateProductRequest request,
    IProductService service) =>
{
    var result = await service.UpdateAsync(id, request);
    
    return result.StatusCode switch
    {
        200 => Results.Ok(result.Data),
        404 => Results.NotFound(new ErrorResponse 
        { 
            Error = result.Message, 
            StatusCode = 404 
        }),
        409 => Results.Conflict(new ErrorResponse 
        { 
            Error = result.Message, 
            StatusCode = 409 
        }),
        _ => Results.Problem(result.Message, statusCode: result.StatusCode)
    };
})
.WithName("UpdateProduct")
.WithTags("Products")
.WithOpenApi(operation =>
{
    operation.Summary = "Update a product";
    operation.Description = "Updates an existing product with partial data (PATCH semantics)";
    
    // Add example
    operation.RequestBody.Content["application/json"].Example = new OpenApiObject
    {
        ["name"] = new OpenApiString("Updated Gaming Laptop"),
        ["price"] = new OpenApiDouble(1199.99),
        ["description"] = new OpenApiString("Now with improved cooling system")
    };
    
    return operation;
})
.RequireAuthorization("ProductEditPolicy")
.Produces<Product>(200)
.ProducesValidationProblem(400)
.ProducesProblem(401)
.ProducesProblem(403)
.ProducesProblem(404)
.ProducesProblem(409)
.ProducesProblem(500);
```

---

## Best Practices Summary

### ✅ DO

1. **Always include XML comments** for public APIs
2. **Provide realistic examples** in documentation
3. **Document all response codes** your endpoint can return
4. **Use consistent naming** across endpoints
5. **Include authentication requirements** clearly
6. **Document deprecated endpoints** with migration path
7. **Specify data types** with examples
8. **Group related endpoints** using tags
9. **Document validation rules** in remarks
10. **Include correlation IDs** in error responses

### ❌ DON'T

1. Don't leave endpoints undocumented
2. Don't use vague descriptions ("Gets data")
3. Don't forget to document error responses
4. Don't expose internal implementation details
5. Don't use technical jargon without explanation
6. Don't forget to update docs when code changes
7. Don't document deprecated endpoints without alternatives
8. Don't omit authentication requirements

---

## Related Documentation

- [Developer Onboarding Guide](./developer-onboarding.md)
- [Result Pattern Guide](../patterns/result-pattern-guide.md)
- [VSA Migration Guide](./vsa-migration-guide.md)
- [Testing Patterns Guide](../patterns/testing-patterns.md)

---

**Last Updated**: 2025-11-20  
**Version**: 1.0  
**Author**: XFramework Development Team
# Result Pattern Guide - XFramework

## Overview

The `Result<T>` pattern is XFramework's standardized way of handling operation outcomes, replacing exception-based error handling with explicit success/failure states. This pattern provides type-safe, predictable error handling while maintaining clean, readable code.

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [Factory Methods](#factory-methods)
3. [Usage in Services](#usage-in-services)
4. [Error Handling Best Practices](#error-handling-best-practices)
5. [Integration with Endpoints](#integration-with-endpoints)
6. [Advanced Patterns](#advanced-patterns)
7. [Railway-Oriented Programming](#railway-oriented-programming)

---

## Core Concepts

### Result Structure

The [`Result<T>`](../../src/Kernel/XFramework.Core/Patterns/Result.cs) record contains:

```csharp
public record Result<T>
{
    public T? Data { get; init; }           // The actual data (null if failed)
    public bool IsSuccess { get; init; }     // Success indicator
    public string? Message { get; init; }    // User-friendly message
    public int StatusCode { get; init; }     // HTTP status code (200, 404, 400, etc.)
    public Dictionary<string, string[]>? Errors { get; init; }  // Validation errors
}
```

### Non-Generic Result

For operations that don't return data:

```csharp
public record Result
{
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public int StatusCode { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
}
```

### Why Use Result Pattern?

**Traditional Exception Approach (❌ Avoid):**
```csharp
public async Task<Wallet> GetWalletAsync(Guid id)
{
    var wallet = await _db.Wallets.FindAsync(id);
    if (wallet == null)
        throw new NotFoundException("Wallet not found"); // Forces caller to handle exceptions
    return wallet;
}

// Caller must use try-catch
try
{
    var wallet = await GetWalletAsync(id);
    // Use wallet
}
catch (NotFoundException ex)
{
    // Handle error
}
```

**Result Pattern Approach (✅ Preferred):**
```csharp
public async Task<Result<Wallet>> GetWalletAsync(Guid id)
{
    var wallet = await _db.Wallets.FindAsync(id);
    if (wallet == null)
        return Result<Wallet>.NotFound("Wallet not found"); // Explicit failure
    return Result<Wallet>.Success(wallet);
}

// Caller uses clean if/else
var result = await GetWalletAsync(id);
if (result.IsSuccess)
{
    var wallet = result.Data;
    // Use wallet
}
else
{
    // Handle error (result.Message, result.StatusCode)
}
```

**Benefits:**
- 🎯 **Explicit**: Success/failure is part of the type signature
- 🔍 **Discoverable**: IntelliSense shows all possible outcomes
- 🛡️ **Type-safe**: Compiler ensures error handling
- 📊 **HTTP-aligned**: StatusCode maps directly to HTTP responses
- 🧪 **Testable**: Easy to test success and failure paths

---

## Factory Methods

### Success Methods

#### Basic Success
```csharp
// With data, default 200 status
var result = Result<Product>.Success(product);

// With data and custom message
var result = Result<Product>.Success(product, "Product created successfully");
```

#### Success with Custom Status Code
```csharp
// Created (201)
var result = Result<Product>.Success(product, 201, "Product created");

// Accepted (202)
var result = Result.Success("Request accepted for processing", 202);
```

### Failure Methods

#### Generic Failure
```csharp
// Default 400 Bad Request
var result = Result<Product>.Failure("Invalid product data");

// Custom status code
var result = Result<Product>.Failure("Internal error occurred", 500);
```

#### Not Found (404)
```csharp
// Default message
var result = Result<Product>.NotFound();

// Custom message
var result = Result<Product>.NotFound($"Product {id} not found");
```

#### Validation Error (400)
```csharp
var errors = new Dictionary<string, string[]>
{
    ["Name"] = new[] { "Name is required", "Name must be at least 3 characters" },
    ["Price"] = new[] { "Price must be positive" }
};

var result = Result<Product>.ValidationError(errors, "Validation failed");
```

#### Unauthorized (401)
```csharp
var result = Result<Product>.Unauthorized("You must be logged in");
```

#### Forbidden (403)
```csharp
var result = Result<Product>.Forbidden("You don't have permission to access this resource");
```

#### Conflict (409)
```csharp
var result = Result<Product>.Conflict("A product with this SKU already exists");
```

---

## Usage in Services

### Pattern 1: Simple CRUD Operation

```csharp
public class ProductService
{
    private readonly DbContext _db;
    private readonly ILogger<ProductService> _logger;

    public async Task<Result<Product>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

            if (product == null)
            {
                _logger.EntityNotFound("Product", id);
                return Result<Product>.NotFound($"Product {id} not found");
            }

            return Result<Product>.Success(product);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("GetProduct", "Product", id, ex.Message, ex);
            return Result<Product>.Failure("An error occurred while retrieving the product", 500);
        }
    }
}
```

### Pattern 2: Create with Validation

```csharp
public async Task<Result<Product>> CreateAsync(
    CreateProductRequest request, 
    CancellationToken ct = default)
{
    try
    {
        // Business rule validation
        if (request.Price <= 0)
        {
            _logger.ValidationFailed("CreateProduct", "Price must be positive");
            return Result<Product>.Failure("Price must be positive", 400);
        }

        // Check for duplicates
        var exists = await _db.Products
            .AnyAsync(p => p.SKU == request.SKU && !p.IsDeleted, ct);

        if (exists)
        {
            _logger.BusinessRuleViolation("CreateProduct", $"SKU {request.SKU} already exists");
            return Result<Product>.Conflict($"A product with SKU {request.SKU} already exists");
        }

        // Create entity
        var product = new Product
        {
            Name = request.Name,
            SKU = request.SKU,
            Price = request.Price
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        _logger.EntityCreated("Product", product.Id);
        return Result<Product>.Success(product, 201, "Product created successfully");
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("CreateProduct", "Product", Guid.Empty, ex.Message, ex);
        return Result<Product>.Failure("An error occurred while creating the product", 500);
    }
}
```

### Pattern 3: Update with Concurrency Handling

```csharp
public async Task<Result<Product>> UpdateAsync(
    Guid id,
    UpdateProductRequest request,
    CancellationToken ct = default)
{
    try
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        if (product == null)
        {
            return Result<Product>.NotFound($"Product {id} not found");
        }

        // Update properties
        product.Name = request.Name;
        product.Price = request.Price;

        await _db.SaveChangesAsync(ct);

        _logger.EntityUpdated("Product", id);
        return Result<Product>.Success(product, "Product updated successfully");
    }
    catch (DbUpdateConcurrencyException ex)
    {
        _logger.ConcurrencyConflict("Product", id);
        return Result<Product>.Conflict("The product was modified by another user. Please refresh and try again");
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("UpdateProduct", "Product", id, ex.Message, ex);
        return Result<Product>.Failure("An error occurred while updating the product", 500);
    }
}
```

### Pattern 4: Delete (Soft Delete)

```csharp
public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
{
    try
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        if (product == null)
        {
            return Result.NotFound($"Product {id} not found");
        }

        // Soft delete
        product.IsDeleted = true;
        await _db.SaveChangesAsync(ct);

        _logger.EntityDeleted("Product", id);
        return Result.Success("Product deleted successfully");
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("DeleteProduct", "Product", id, ex.Message, ex);
        return Result.Failure("An error occurred while deleting the product", 500);
    }
}
```

### Pattern 5: Complex Business Operation

```csharp
public async Task<Result> TransferInventoryAsync(
    TransferInventoryRequest request,
    CancellationToken ct = default)
{
    try
    {
        // Validation
        if (request.Quantity <= 0)
        {
            return Result.Failure("Quantity must be positive", 400);
        }

        // Fetch source and destination
        var source = await _db.Warehouses.FindAsync(request.SourceWarehouseId);
        var destination = await _db.Warehouses.FindAsync(request.DestinationWarehouseId);

        if (source == null || destination == null)
        {
            return Result.NotFound("One or both warehouses not found");
        }

        // Business rule: Check stock availability
        var inventory = await _db.Inventory
            .FirstOrDefaultAsync(i => 
                i.WarehouseId == request.SourceWarehouseId && 
                i.ProductId == request.ProductId, ct);

        if (inventory == null || inventory.Quantity < request.Quantity)
        {
            _logger.InsufficientInventory(request.ProductId, request.Quantity, inventory?.Quantity ?? 0);
            return Result.Failure("Insufficient inventory", 400);
        }

        // Execute transfer
        inventory.Quantity -= request.Quantity;

        var destinationInventory = await _db.Inventory
            .FirstOrDefaultAsync(i => 
                i.WarehouseId == request.DestinationWarehouseId && 
                i.ProductId == request.ProductId, ct)
            ?? new Inventory 
            { 
                WarehouseId = request.DestinationWarehouseId,
                ProductId = request.ProductId,
                Quantity = 0
            };

        destinationInventory.Quantity += request.Quantity;

        // Create transfer record
        _db.InventoryTransfers.Add(new InventoryTransfer
        {
            SourceWarehouseId = request.SourceWarehouseId,
            DestinationWarehouseId = request.DestinationWarehouseId,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            TransferredAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        _logger.InventoryTransferred(request.ProductId, request.SourceWarehouseId, request.DestinationWarehouseId, request.Quantity);
        return Result.Success("Inventory transferred successfully");
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("TransferInventory", "Inventory", request.ProductId, ex.Message, ex);
        return Result.Failure("An error occurred during the transfer", 500);
    }
}
```

---

## Error Handling Best Practices

### 1. Use Specific Error Methods

```csharp
// ❌ Bad: Generic failure for everything
if (product == null)
    return Result<Product>.Failure("Not found", 404);

// ✅ Good: Use specific factory method
if (product == null)
    return Result<Product>.NotFound($"Product {id} not found");
```

### 2. Provide Helpful Error Messages

```csharp
// ❌ Bad: Vague message
return Result.Failure("Invalid data");

// ✅ Good: Specific and actionable
return Result.Failure("Product name must be between 3 and 100 characters", 400);
```

### 3. Use Validation Errors for Multiple Field Errors

```csharp
// ❌ Bad: Single message for multiple errors
if (errors.Any())
    return Result<Product>.Failure("Validation failed");

// ✅ Good: Structured validation errors
var validationErrors = new Dictionary<string, string[]>
{
    ["Name"] = new[] { "Name is required" },
    ["Price"] = new[] { "Price must be positive" },
    ["SKU"] = new[] { "SKU must be unique" }
};
return Result<Product>.ValidationError(validationErrors);
```

### 4. Log Before Returning Errors

```csharp
// ✅ Good: Always log errors
if (product == null)
{
    _logger.EntityNotFound("Product", id);  // Log it
    return Result<Product>.NotFound($"Product {id} not found");
}
```

### 5. Catch Specific Exceptions

```csharp
try
{
    // Operation
}
catch (DbUpdateConcurrencyException ex)
{
    _logger.ConcurrencyConflict("Product", id);
    return Result<Product>.Conflict("Concurrent modification detected");
}
catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 2627)
{
    _logger.UniqueConstraintViolation("Product", "SKU");
    return Result<Product>.Conflict("A product with this SKU already exists");
}
catch (Exception ex)
{
    _logger.OperationFailed("UpdateProduct", "Product", id, ex.Message, ex);
    return Result<Product>.Failure("An unexpected error occurred", 500);
}
```

---

## Integration with Endpoints

### Pattern 1: Minimal API with Result

```csharp
app.MapGet("/api/products/{id:guid}", async (
    Guid id,
    IProductService productService) =>
{
    var result = await productService.GetByIdAsync(id);
    
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.NotFound(new { error = result.Message });
})
.WithName("GetProduct")
.WithTags("Products")
.Produces<Product>(200)
.ProducesProblem(404)
.ProducesProblem(500);
```

### Pattern 2: Helper Method for Standard Response Mapping

```csharp
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        return result.StatusCode switch
        {
            200 => Results.Ok(result.Data),
            201 => Results.Created($"/api/resource/{result.Data?.GetType().GetProperty("Id")?.GetValue(result.Data)}", result.Data),
            204 => Results.NoContent(),
            400 => Results.BadRequest(new { error = result.Message, errors = result.Errors }),
            401 => Results.Unauthorized(),
            403 => Results.Forbid(),
            404 => Results.NotFound(new { error = result.Message }),
            409 => Results.Conflict(new { error = result.Message }),
            _ => Results.Problem(result.Message, statusCode: result.StatusCode)
        };
    }
}

// Usage
app.MapPost("/api/products", async (
    CreateProductRequest request,
    IProductService productService) =>
{
    var result = await productService.CreateAsync(request);
    return result.ToHttpResult();
});
```

### Pattern 3: Detailed Error Responses

```csharp
app.MapPost("/api/products", async (
    CreateProductRequest request,
    IProductService productService) =>
{
    var result = await productService.CreateAsync(request);
    
    if (result.IsSuccess)
    {
        return Results.Created($"/api/products/{result.Data.Id}", new
        {
            data = result.Data,
            message = result.Message ?? "Product created successfully"
        });
    }

    // Error response with details
    return Results.Problem(
        title: "Product Creation Failed",
        detail: result.Message,
        statusCode: result.StatusCode,
        extensions: result.Errors != null 
            ? new Dictionary<string, object?> { ["errors"] = result.Errors }
            : null
    );
})
.WithName("CreateProduct")
.WithTags("Products")
.Accepts<CreateProductRequest>("application/json")
.Produces<Product>(201)
.ProducesValidationProblem(400)
.ProducesProblem(409)
.ProducesProblem(500);
```

---

## Advanced Patterns

### Pattern 1: Map - Transform Success Data

```csharp
// Transform product to DTO
var productResult = await productService.GetByIdAsync(id);

var dtoResult = productResult.Map(product => new ProductDto
{
    Id = product.Id,
    Name = product.Name,
    Price = product.Price,
    FormattedPrice = $"${product.Price:N2}"
});

// Result<Product> → Result<ProductDto>
```

### Pattern 2: Bind - Chain Operations

```csharp
// Chain multiple operations
var result = await productService.GetByIdAsync(id)
    .Bind(async product => await inventoryService.ReserveStockAsync(product.Id, quantity))
    .Bind(async reservation => await orderService.CreateOrderAsync(reservation));

// Stops at first failure
```

### Pattern 3: Match - Handle Both Cases

```csharp
var message = result.Match(
    onSuccess: product => $"Product {product.Name} found",
    onFailure: error => $"Error: {error}"
);
```

### Pattern 4: OnSuccess/OnFailure - Side Effects

```csharp
var result = await productService.CreateAsync(request);

result
    .OnSuccess(product => _logger.LogInformation("Created product {Id}", product.Id))
    .OnFailure(error => _logger.LogWarning("Failed to create product: {Error}", error));

return result;
```

### Pattern 5: Ensure - Add Validation

```csharp
var result = await productService.GetByIdAsync(id);

var validatedResult = result.Ensure(
    product => product.IsActive,
    "Product is not active"
);

// Returns failure if predicate is false
```

### Pattern 6: Combine - Merge Multiple Results

```csharp
var productResults = new[]
{
    await productService.GetByIdAsync(id1),
    await productService.GetByIdAsync(id2),
    await productService.GetByIdAsync(id3)
};

var combinedResult = productResults.Combine();
// Result<IEnumerable<Product>> - fails if any individual result failed
```

---

## Railway-Oriented Programming

The Result pattern enables Railway-Oriented Programming (ROP), where operations flow on a "success track" or "failure track":

```csharp
public async Task<Result<Order>> ProcessOrderAsync(CreateOrderRequest request)
{
    return await ValidateRequest(request)
        .Bind(async validReq => await ReserveInventory(validReq))
        .Bind(async reservation => await CreateOrder(reservation))
        .Bind(async order => await ProcessPayment(order))
        .Bind(async order => await SendConfirmationEmail(order))
        .OnSuccess(order => _logger.OrderCompleted(order.Id))
        .OnFailure(error => _logger.OrderFailed(error));
}

// Each step returns Result<T>
// First failure short-circuits the chain
// Success flows through all steps
```

**Visual Flow:**
```
ValidateRequest ──Success──> ReserveInventory ──Success──> CreateOrder
      │                             │                          │
   Failure                       Failure                    Failure
      │                             │                          │
      └─────────────────────────────┴──────────────────────────┘
                                    │
                            Return Failure Result
```

---

## Related Documentation

- [VSA Migration Guide](../guides/vsa-migration-guide.md)
- [Partial Class Override Pattern](./partial-class-pattern.md)
- [Testing Patterns Guide](./testing-patterns.md)
- [Logging Standards](../../docs/standards/logging-standards.md)

---

**Last Updated**: 2025-11-20  
**Version**: 1.0  
**Author**: XFramework Development Team
# VSA Migration Guide - XFramework

## Overview

This guide provides step-by-step instructions for migrating XFramework modules from **CQRS/MediatR** architecture to **Vertical Slice Architecture (VSA)** with direct service injection. This migration improves performance, reduces complexity, and makes the codebase more maintainable.

## Table of Contents

1. [Migration Goals](#migration-goals)
2. [Architecture Comparison](#architecture-comparison)
3. [Step-by-Step Migration Process](#step-by-step-migration-process)
4. [Before/After Examples](#beforeafter-examples)
5. [Common Pitfalls and Solutions](#common-pitfalls-and-solutions)
6. [Migration Checklist](#migration-checklist)
7. [Testing Your Migration](#testing-your-migration)

---

## Migration Goals

### What We're Removing
- ❌ `IRequestHandler<TRequest, TResponse>`
- ❌ `IMediator` / `mediator.Send()`
- ❌ Generic command/query wrappers (`Create<T>`, `Get<T>`, `GetList<T>`)
- ❌ Generic handlers (`CreateHandler<T>`, `GetHandler<T>`)
- ❌ MediatR pipeline behaviors
- ❌ MediatR DI registrations

### What We're Creating
- ✅ Direct service injection in endpoints
- ✅ [`Result<T>`](../../src/Kernel/XFramework.Core/Patterns/Result.cs) pattern for all operations
- ✅ Partial class pattern for extensibility
- ✅ Virtual methods for override capability
- ✅ Source generators for CRUD boilerplate

---

## Architecture Comparison

### Old: CQRS with MediatR

```
Controller/Endpoint
    ↓ (mediator.Send)
Command/Query Handler
    ↓
Repository/DbContext
    ↓
Database
```

**Problems:**
- Extra layer of indirection
- Harder to debug (reflection-based)
- More files to maintain
- Performance overhead

### New: VSA with Direct Services

```
Controller/Endpoint
    ↓ (Direct injection)
Service Layer
    ↓
DbContext
    ↓
Database
```

**Benefits:**
- Direct, type-safe calls
- Easy to debug and trace
- Less boilerplate
- Better performance

---

## Step-by-Step Migration Process

### Step 1: Analyze Current Module Structure

Before starting, document your current module's:
- Commands and queries
- Handlers
- Endpoints
- Custom business logic

**Example Analysis:**
```
Wallets Module:
├── Commands/
│   ├── IncrementWalletRequest.cs
│   └── IncrementWalletHandler.cs
├── Queries/
│   ├── GetWalletRequest.cs
│   └── GetWalletHandler.cs
└── Endpoints/
    └── WalletEndpoints.cs
```

### Step 2: Create Service Interface

Create a new service interface defining all operations:

```csharp
// File: Wallets.Core/Services/IWalletService.cs
namespace Wallets.Core.Services;

/// <summary>
/// Service for managing wallet operations.
/// </summary>
public interface IWalletService
{
    /// <summary>
    /// Creates a new wallet for a credential.
    /// </summary>
    Task<Result<Wallet>> CreateWalletAsync(
        Guid credentialId,
        Guid walletTypeId,
        decimal initialBalance,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a wallet by ID.
    /// </summary>
    Task<Result<Wallet>> GetWalletAsync(
        Guid walletId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments wallet balance.
    /// </summary>
    Task<Result> IncrementBalanceAsync(
        IncrementWalletRequest request,
        CancellationToken cancellationToken = default);

    // ... other methods
}
```

### Step 3: Implement Service Class

Create the service implementation, consolidating handler logic:

```csharp
// File: Wallets.Core/Services/WalletService.cs
namespace Wallets.Core.Services;

public class WalletService : IWalletService
{
    private readonly DbContext _dbContext;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        DbContext dbContext,
        ILogger<WalletService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result> IncrementBalanceAsync(
        IncrementWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validation
            if (request.TotalAmount <= 0)
            {
                _logger.ValidationFailed("IncrementWallet", "Invalid amount");
                return Result.Failure("Invalid increment amount", 400);
            }

            // Fetch wallet
            var wallet = await _dbContext.Set<Wallet>()
                .FirstOrDefaultAsync(w => w.Id == request.WalletId, cancellationToken);

            if (wallet is null)
            {
                _logger.EntityNotFound("Wallet", request.WalletId);
                return Result.NotFound("Wallet not found");
            }

            // Business logic
            wallet.Balance += request.TotalAmount;
            wallet.TransferableBalance += request.TotalAmount;

            // Create transaction record
            var transaction = new WalletTransaction
            {
                WalletId = wallet.Id,
                Amount = request.TotalAmount,
                TransactionType = TransactionType.Credit
            };

            _dbContext.Set<WalletTransaction>().Add(transaction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.WalletIncremented(wallet.Id, request.TotalAmount, "Primary", wallet.Balance);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("IncrementWallet", "Wallet", request.WalletId, ex.Message, ex);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }
}
```

### Step 4: Update Dependency Injection

Replace MediatR registration with service registration:

```csharp
// File: Wallets.Api/Installers/ServicesInstaller.cs
public class ServicesInstaller : IInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        // ❌ OLD: Remove MediatR
        // services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // ✅ NEW: Register services directly
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IBatchWalletService, BatchWalletService>();
    }
}
```

### Step 5: Update Endpoints

Replace MediatR calls with direct service injection:

**Before (MediatR):**
```csharp
app.MapPost("/api/wallets/increment", async (
    IMediator mediator,
    IncrementWalletRequest request) =>
{
    var result = await mediator.Send(request);
    return result.IsSuccess 
        ? Results.Ok(result) 
        : Results.BadRequest(result.Message);
});
```

**After (Direct Service):**
```csharp
app.MapPost("/api/wallets/increment", async (
    IWalletService walletService,  // Direct injection
    IncrementWalletRequest request) =>
{
    var result = await walletService.IncrementBalanceAsync(request);
    return result.IsSuccess 
        ? Results.Ok(result) 
        : Results.BadRequest(result.Message);
})
.WithName("IncrementWallet")
.WithTags("Wallets")
.ProducesProblem(400)
.ProducesProblem(404)
.ProducesProblem(500);
```

### Step 6: Delete Old CQRS Files

After verifying everything works, remove:
```bash
# Commands and handlers
rm -rf Commands/
rm -rf Handlers/

# Or selectively delete
rm IncrementWalletRequest.cs
rm IncrementWalletHandler.cs
rm GetWalletRequest.cs
rm GetWalletHandler.cs
```

### Step 7: Update Tests

Update tests to use services directly instead of handlers:

**Before:**
```csharp
[Fact]
public async Task IncrementWallet_Should_Increase_Balance()
{
    // Arrange
    var handler = new IncrementWalletHandler(_dbContext, _logger);
    var request = new IncrementWalletRequest { /* ... */ };

    // Act
    var result = await handler.Handle(request, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
}
```

**After:**
```csharp
[Fact]
public async Task IncrementWallet_Should_Increase_Balance()
{
    // Arrange
    var service = new WalletService(_dbContext, _logger);
    var request = new IncrementWalletRequest { /* ... */ };

    // Act
    var result = await service.IncrementBalanceAsync(request);

    // Assert
    Assert.True(result.IsSuccess);
}
```

---

## Before/After Examples

### Example 1: Simple CRUD Operation

#### Before: CQRS with MediatR

**Command:**
```csharp
public record CreateProductRequest(string Name, decimal Price) : IRequest<Result<Product>>;
```

**Handler:**
```csharp
public class CreateProductHandler : IRequestHandler<CreateProductRequest, Result<Product>>
{
    private readonly DbContext _db;
    
    public async Task<Result<Product>> Handle(CreateProductRequest request, CancellationToken ct)
    {
        var product = new Product { Name = request.Name, Price = request.Price };
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return Result<Product>.Success(product);
    }
}
```

**Endpoint:**
```csharp
app.MapPost("/products", async (IMediator mediator, CreateProductRequest request) =>
{
    var result = await mediator.Send(request);
    return Results.Created($"/products/{result.Data.Id}", result);
});
```

#### After: VSA with Direct Services

**Service:**
```csharp
public class ProductService
{
    private readonly DbContext _db;
    private readonly ILogger<ProductService> _logger;
    
    public virtual async Task<Result<Product>> CreateAsync(
        CreateProductRequest request, 
        CancellationToken ct = default)
    {
        try
        {
            var product = new Product { Name = request.Name, Price = request.Price };
            _db.Products.Add(product);
            await _db.SaveChangesAsync(ct);
            
            _logger.EntityCreated("Product", product.Id);
            return Result<Product>.Success(product);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateProduct", "Product", Guid.Empty, ex.Message, ex);
            return Result<Product>.Failure("Failed to create product", 500);
        }
    }
}
```

**Endpoint:**
```csharp
app.MapPost("/products", async (
    ProductService service,  // Direct injection
    CreateProductRequest request) =>
{
    var result = await service.CreateAsync(request);
    return result.IsSuccess
        ? Results.Created($"/products/{result.Data.Id}", result.Data)
        : Results.BadRequest(result.Message);
});
```

### Example 2: Complex Business Logic

#### Before: Multiple Handlers

```csharp
// Transfer involves multiple handlers
public class TransferWalletHandler : IRequestHandler<TransferRequest, Result>
{
    public async Task<Result> Handle(TransferRequest request, CancellationToken ct)
    {
        // Complex transfer logic scattered across handlers
        await _mediator.Send(new DecrementWalletRequest(/*...*/));
        await _mediator.Send(new IncrementWalletRequest(/*...*/));
        await _mediator.Send(new CreateTransactionRequest(/*...*/));
        return Result.Success();
    }
}
```

#### After: Single Service Method

```csharp
public class WalletService
{
    public async Task<Result> TransferAsync(
        TransferWalletRequest request,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.Wallet.StartActivity("Wallet.Transfer");
        
        try
        {
            // All logic in one place, easier to maintain
            var senderWallet = await GetWalletAsync(request.SenderId);
            var recipientWallet = await GetWalletAsync(request.RecipientId);
            
            // Validate
            if (senderWallet.Balance < request.Amount)
                return Result.Failure("Insufficient balance", 400);
            
            // Execute transfer atomically
            senderWallet.Balance -= request.Amount;
            recipientWallet.Balance += request.Amount;
            
            // Create transaction records
            _db.WalletTransactions.Add(new WalletTransaction { /* ... */ });
            
            await _db.SaveChangesAsync(ct);
            
            _logger.WalletTransfer(senderWallet.Id, recipientWallet.Id, request.Amount);
            return Result.Success();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.OperationFailed("Transfer", "Wallet", request.SenderId, ex.Message, ex);
            return Result.Failure("Transfer failed", 500);
        }
    }
}
```

---

## Common Pitfalls and Solutions

### Pitfall 1: Forgetting to Remove MediatR Dependencies

**Problem:**
```csharp
// Still references MediatR
public class WalletEndpoints
{
    public static void MapEndpoints(WebApplication app, IMediator mediator) // ❌
    {
        // ...
    }
}
```

**Solution:**
```csharp
// Remove IMediator parameter
public class WalletEndpoints
{
    public static void MapEndpoints(WebApplication app) // ✅
    {
        // Services are injected per-endpoint
    }
}
```

### Pitfall 2: Not Using Result<T> Pattern

**Problem:**
```csharp
// Throwing exceptions for business logic failures
public async Task<Wallet> GetWalletAsync(Guid id)
{
    var wallet = await _db.Wallets.FindAsync(id);
    if (wallet == null)
        throw new NotFoundException("Wallet not found"); // ❌
    return wallet;
}
```

**Solution:**
```csharp
// Use Result pattern
public async Task<Result<Wallet>> GetWalletAsync(Guid id)
{
    var wallet = await _db.Wallets.FindAsync(id);
    if (wallet == null)
        return Result<Wallet>.NotFound("Wallet not found"); // ✅
    return Result<Wallet>.Success(wallet);
}
```

### Pitfall 3: Missing AsNoTracking for Read Operations

**Problem:**
```csharp
// Tracking entities unnecessarily
var wallet = await _db.Wallets
    .FirstOrDefaultAsync(w => w.Id == id); // ❌ Tracking enabled
```

**Solution:**
```csharp
// Explicit no-tracking for read-only operations
var wallet = await _db.Wallets
    .AsNoTracking() // ✅
    .FirstOrDefaultAsync(w => w.Id == id);
```

### Pitfall 4: Not Invalidating Cache After Mutations

**Problem:**
```csharp
public async Task<Result<Wallet>> UpdateAsync(Guid id, UpdateWalletRequest request)
{
    // Update wallet
    await _db.SaveChangesAsync();
    return Result<Wallet>.Success(wallet); // ❌ Cache not invalidated
}
```

**Solution:**
```csharp
public async Task<Result<Wallet>> UpdateAsync(Guid id, UpdateWalletRequest request)
{
    // Update wallet
    await _db.SaveChangesAsync();
    
    // Invalidate cache
    await _cacheService.RemoveAsync($"wallet:{id}"); // ✅
    await _cacheService.RemoveByPrefixAsync("wallet:"); // ✅
    
    return Result<Wallet>.Success(wallet);
}
```

### Pitfall 5: Manually Setting Audit Fields

**Problem:**
```csharp
var entity = new Product
{
    Name = request.Name,
    CreatedAt = DateTime.UtcNow, // ❌ Set manually
    CreatedBy = currentUserId     // ❌ Set manually
};
```

**Solution:**
```csharp
var entity = new Product
{
    Name = request.Name
    // ✅ CreatedAt, CreatedBy, UpdatedAt handled by AuditInterceptor
};
```

---

## Migration Checklist

Use this checklist for each module migration:

### Planning Phase
- [ ] Document all current commands and queries
- [ ] Identify custom business logic vs. generic CRUD
- [ ] List all endpoints that need updating
- [ ] Review dependencies between handlers

### Implementation Phase
- [ ] Create service interface (`I{Module}Service.cs`)
- [ ] Create service implementation (`{Module}Service.cs`)
- [ ] Migrate handler logic to service methods
- [ ] Update all endpoints to use direct service injection
- [ ] Update DI registrations (remove MediatR, add services)
- [ ] Add structured logging with [`LoggerMessage`](../../docs/standards/logging-standards.md)
- [ ] Add OpenTelemetry tracing for key operations
- [ ] Implement cache invalidation where needed

### Testing Phase
- [ ] Update unit tests to test services directly
- [ ] Update integration tests for endpoints
- [ ] Test error handling scenarios
- [ ] Verify logging output
- [ ] Check OpenTelemetry traces
- [ ] Performance test critical paths

### Cleanup Phase
- [ ] Delete old command/query files
- [ ] Delete old handler files
- [ ] Remove MediatR package references
- [ ] Update documentation
- [ ] Code review

### Verification Phase
- [ ] Run `dotnet build` - must succeed
- [ ] Run `dotnet test` - all tests must pass
- [ ] Test manually via Swagger UI
- [ ] Check logs for proper structured logging
- [ ] Verify cache behavior
- [ ] Monitor performance metrics

---

## Testing Your Migration

### Unit Testing Services

```csharp
public class WalletServiceTests
{
    private readonly DbContext _dbContext;
    private readonly ILogger<WalletService> _logger;
    private readonly WalletService _service;

    public WalletServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new DbContext(options);
        _logger = new NullLogger<WalletService>();
        _service = new WalletService(_dbContext, _logger);
    }

    [Fact]
    public async Task IncrementBalance_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var wallet = new Wallet { Id = Guid.NewGuid(), Balance = 100 };
        _dbContext.Wallets.Add(wallet);
        await _dbContext.SaveChangesAsync();
        
        var request = new IncrementWalletRequest
        {
            WalletId = wallet.Id,
            TotalAmount = 50
        };

        // Act
        var result = await _service.IncrementBalanceAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(150, wallet.Balance);
    }

    [Fact]
    public async Task IncrementBalance_InvalidAmount_ReturnsFailure()
    {
        // Arrange
        var request = new IncrementWalletRequest
        {
            WalletId = Guid.NewGuid(),
            TotalAmount = -50 // Invalid
        };

        // Act
        var result = await _service.IncrementBalanceAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Invalid", result.Message);
    }
}
```

### Integration Testing Endpoints

```csharp
public class WalletEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public WalletEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IncrementWallet_ValidRequest_Returns200()
    {
        // Arrange
        var request = new IncrementWalletRequest
        {
            WalletId = Guid.NewGuid(),
            TotalAmount = 100
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/wallets/increment", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Result>();
        Assert.True(result.IsSuccess);
    }
}
```

---

## Next Steps

After completing the migration:

1. **Monitor Performance**: Check that response times have improved
2. **Review Logs**: Verify structured logging is working correctly
3. **Update Documentation**: Update API docs and internal wikis
4. **Team Training**: Share lessons learned with the team
5. **Plan Next Module**: Apply learnings to the next module migration

## Related Documentation

- [Result Pattern Guide](../patterns/result-pattern-guide.md)
- [Partial Class Override Pattern](../patterns/partial-class-pattern.md)
- [Caching Strategy Guide](../patterns/caching-strategy.md)
- [Testing Patterns Guide](../patterns/testing-patterns.md)
- [Logging Standards](../../docs/standards/logging-standards.md)
- [OpenTelemetry Guide](../../docs/observability/opentelemetry-guide.md)

---

**Last Updated**: 2025-11-20  
**Version**: 1.0  
**Author**: XFramework Development Team
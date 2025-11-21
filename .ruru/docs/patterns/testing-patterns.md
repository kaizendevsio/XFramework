# Testing Patterns Guide - XFramework

## Overview

This guide provides comprehensive testing strategies for XFramework's VSA (Vertical Slice Architecture) with Result<T> pattern. Learn how to effectively test services, endpoints, caching behavior, and more.

## Table of Contents

1. [Testing Philosophy](#testing-philosophy)
2. [Unit Testing Services](#unit-testing-services)
3. [Integration Testing Endpoints](#integration-testing-endpoints)
4. [Testing Caching Behavior](#testing-caching-behavior)
5. [Mock and Stub Patterns](#mock-and-stub-patterns)
6. [Testing Result<T> Pattern](#testing-resultt-pattern)
7. [Example Test Cases](#example-test-cases)

---

## Testing Philosophy

### Test Pyramid for XFramework

```
         ╱‾‾‾‾‾‾‾‾╲
        ╱ E2E Tests ╲       ← Few, critical user journeys
       ╱‾‾‾‾‾‾‾‾‾‾‾‾╲
      ╱ Integration  ╲      ← API endpoints, database
     ╱‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾╲
    ╱   Unit Tests    ╲     ← Many, fast, isolated
   ╱‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾╲
```

### Testing Priorities

1. **Unit Tests (70%)**: Service methods, business logic, Result<T> handling
2. **Integration Tests (25%)**: API endpoints, database operations, caching
3. **E2E Tests (5%)**: Critical user workflows (optional for most features)

### XFramework Testing Stack

```csharp
// Core Testing Framework
- xUnit (test runner)
- FluentAssertions (readable assertions)
- Moq (mocking framework)
- AutoFixture (test data generation)

// Integration Testing
- WebApplicationFactory<T> (in-memory API host)
- EntityFrameworkCore.InMemory (test database)
- TestContainers (optional: real database testing)

// Coverage
- Coverlet (code coverage)
```

---

## Unit Testing Services

### Setup: Base Test Class

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public abstract class ServiceTestBase<TService> : IDisposable
{
    protected readonly DbContext DbContext;
    protected readonly Mock<ICacheService> MockCache;
    protected readonly ILogger<TService> Logger;

    protected ServiceTestBase()
    {
        // In-memory database
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        DbContext = new DbContext(options);

        // Mock cache service
        MockCache = new Mock<ICacheService>();

        // Null logger (or use ITestOutputHelper for debugging)
        Logger = new NullLogger<TService>();
    }

    public void Dispose()
    {
        DbContext?.Dispose();
    }

    protected void SeedDatabase(params object[] entities)
    {
        DbContext.AddRange(entities);
        DbContext.SaveChanges();
    }
}
```

### Pattern 1: Testing CRUD Operations

```csharp
public class ProductServiceTests : ServiceTestBase<ProductService>
{
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _service = new ProductService(DbContext, MockCache.Object, Logger);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsSuccessWithProduct()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            Name = "Test Product",
            SKU = "TEST-001",
            Price = 99.99m
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data.Name.Should().Be("Test Product");
        result.Data.Id.Should().NotBeEmpty();

        // Verify database was updated
        var productInDb = await DbContext.Set<Product>().FindAsync(result.Data.Id);
        productInDb.Should().NotBeNull();
        productInDb.Name.Should().Be("Test Product");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingProduct_ReturnsSuccess()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Existing Product",
            SKU = "EXIST-001",
            Price = 49.99m
        };
        SeedDatabase(product);

        // Act
        var result = await _service.GetByIdAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Id.Should().Be(product.Id);
        result.Data.Name.Should().Be("Existing Product");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentProduct_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.GetByIdAsync(nonExistentId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Contain("not found");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_ReturnsSuccessWithUpdatedProduct()
    {
        // Arrange
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Price = 50m
        };
        SeedDatabase(product);

        var request = new UpdateProductRequest
        {
            Name = "Updated Name",
            Price = 75m
        };

        // Act
        var result = await _service.UpdateAsync(product.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Name.Should().Be("Updated Name");
        result.Data.Price.Should().Be(75m);

        // Verify database was updated
        var updatedProduct = await DbContext.Set<Product>().FindAsync(product.Id);
        updatedProduct.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_ExistingProduct_SetsIsDeletedTrue()
    {
        // Arrange
        var product = new Product { Id = Guid.NewGuid(), Name = "To Delete" };
        SeedDatabase(product);

        // Act
        var result = await _service.DeleteAsync(product.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify soft delete
        var deletedProduct = await DbContext.Set<Product>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == product.Id);
        
        deletedProduct.Should().NotBeNull();
        deletedProduct.IsDeleted.Should().BeTrue();
    }
}
```

### Pattern 2: Testing Business Logic

```csharp
public class WalletServiceTests : ServiceTestBase<WalletService>
{
    private readonly WalletService _service;
    private readonly Mock<ITenantService> _mockTenantService;

    public WalletServiceTests()
    {
        _mockTenantService = new Mock<ITenantService>();
        _service = new WalletService(
            DbContext, 
            _mockTenantService.Object, 
            Logger);
    }

    [Fact]
    public async Task IncrementBalance_ValidRequest_IncreasesBalance()
    {
        // Arrange
        var tenant = new Tenant { Id = Guid.NewGuid() };
        var wallet = new Wallet 
        { 
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Balance = 100m,
            TransferableBalance = 100m
        };
        SeedDatabase(tenant, wallet);

        _mockTenantService.Setup(s => s.GetTenant(tenant.Id))
            .ReturnsAsync(tenant);

        var request = new IncrementWalletRequest
        {
            WalletId = wallet.Id,
            TotalAmount = 50m,
            Metadata = new RequestMetadata { TenantId = tenant.Id }
        };

        // Act
        var result = await _service.IncrementBalanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify balance updated
        var updatedWallet = await DbContext.Set<Wallet>().FindAsync(wallet.Id);
        updatedWallet.Balance.Should().Be(150m);
        updatedWallet.TransferableBalance.Should().Be(150m);
    }

    [Fact]
    public async Task IncrementBalance_NegativeAmount_ReturnsFailure()
    {
        // Arrange
        var request = new IncrementWalletRequest
        {
            WalletId = Guid.NewGuid(),
            TotalAmount = -50m, // Invalid
            Metadata = new RequestMetadata { TenantId = Guid.NewGuid() }
        };

        // Act
        var result = await _service.IncrementBalanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("Invalid");
    }

    [Fact]
    public async Task IncrementBalance_BelowMinTransferRule_ReturnsFailure()
    {
        // Arrange
        var tenant = new Tenant { Id = Guid.NewGuid() };
        var wallet = new Wallet 
        { 
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Balance = 100m,
            MinTransferRule = 50m // Minimum amount
        };
        SeedDatabase(tenant, wallet);

        _mockTenantService.Setup(s => s.GetTenant(tenant.Id))
            .ReturnsAsync(tenant);

        var request = new IncrementWalletRequest
        {
            WalletId = wallet.Id,
            TotalAmount = 25m, // Below minimum
            Metadata = new RequestMetadata { TenantId = tenant.Id }
        };

        // Act
        var result = await _service.IncrementBalanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("at least");
    }

    [Theory]
    [InlineData(50, 150)]
    [InlineData(100, 200)]
    [InlineData(0.01, 100.01)]
    public async Task IncrementBalance_VariousAmounts_UpdatesBalanceCorrectly(
        decimal amount, 
        decimal expectedBalance)
    {
        // Arrange
        var tenant = new Tenant { Id = Guid.NewGuid() };
        var wallet = new Wallet 
        { 
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Balance = 100m,
            TransferableBalance = 100m
        };
        SeedDatabase(tenant, wallet);

        _mockTenantService.Setup(s => s.GetTenant(tenant.Id))
            .ReturnsAsync(tenant);

        var request = new IncrementWalletRequest
        {
            WalletId = wallet.Id,
            TotalAmount = amount,
            Metadata = new RequestMetadata { TenantId = tenant.Id }
        };

        // Act
        var result = await _service.IncrementBalanceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedWallet = await DbContext.Set<Wallet>().FindAsync(wallet.Id);
        updatedWallet.Balance.Should().Be(expectedBalance);
    }
}
```

---

## Integration Testing Endpoints

### Setup: WebApplicationFactory

```csharp
public class CustomWebApplicationFactory<TProgram> 
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real database
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add in-memory database
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });

            // Override cache service
            services.AddSingleton<ICacheService, InMemoryCacheService>();

            // Build service provider
            var sp = services.BuildServiceProvider();

            // Seed test data
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedTestData(db);
        });
    }

    private static void SeedTestData(AppDbContext db)
    {
        db.Products.AddRange(
            new Product { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Product 1", Price = 10m },
            new Product { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Product 2", Price = 20m }
        );
        db.SaveChanges();
    }
}
```

### Pattern 1: Testing GET Endpoints

```csharp
public class ProductEndpointsTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProductEndpointsTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProduct_ExistingId_Returns200WithProduct()
    {
        // Arrange
        var productId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var response = await _client.GetAsync($"/api/products/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var product = await response.Content.ReadFromJsonAsync<Product>();
        product.Should().NotBeNull();
        product.Id.Should().Be(productId);
        product.Name.Should().Be("Product 1");
    }

    [Fact]
    public async Task GetProduct_NonExistentId_Returns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/products/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductList_DefaultParams_ReturnsListWithProducts()
    {
        // Act
        var response = await _client.GetAsync("/api/products?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var products = await response.Content.ReadFromJsonAsync<List<Product>>();
        products.Should().NotBeNull();
        products.Should().HaveCountGreaterThan(0);
    }
}
```

### Pattern 2: Testing POST Endpoints

```csharp
[Fact]
public async Task CreateProduct_ValidRequest_Returns201WithProduct()
{
    // Arrange
    var request = new CreateProductRequest
    {
        Name = "New Product",
        SKU = "NEW-001",
        Price = 99.99m
    };

    // Act
    var response = await _client.PostAsJsonAsync("/api/products", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    
    var product = await response.Content.ReadFromJsonAsync<Product>();
    product.Should().NotBeNull();
    product.Name.Should().Be("New Product");
    product.Id.Should().NotBeEmpty();

    // Verify Location header
    response.Headers.Location.Should().NotBeNull();
    response.Headers.Location.ToString().Should().Contain($"/api/products/{product.Id}");
}

[Fact]
public async Task CreateProduct_InvalidData_Returns400WithValidationErrors()
{
    // Arrange
    var request = new CreateProductRequest
    {
        Name = "", // Invalid: empty name
        Price = -10m // Invalid: negative price
    };

    // Act
    var response = await _client.PostAsJsonAsync("/api/products", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    
    var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
    problemDetails.Should().NotBeNull();
    problemDetails.Errors.Should().ContainKey("Name");
    problemDetails.Errors.Should().ContainKey("Price");
}
```

### Pattern 3: Testing PUT/PATCH Endpoints

```csharp
[Fact]
public async Task UpdateProduct_ValidRequest_Returns200WithUpdatedProduct()
{
    // Arrange
    var productId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var request = new UpdateProductRequest
    {
        Name = "Updated Product",
        Price = 15m
    };

    // Act
    var response = await _client.PutAsJsonAsync($"/api/products/{productId}", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    
    var product = await response.Content.ReadFromJsonAsync<Product>();
    product.Name.Should().Be("Updated Product");
    product.Price.Should().Be(15m);
}

[Fact]
public async Task UpdateProduct_NonExistentId_Returns404()
{
    // Arrange
    var nonExistentId = Guid.NewGuid();
    var request = new UpdateProductRequest
    {
        Name = "Updated Product",
        Price = 15m
    };

    // Act
    var response = await _client.PutAsJsonAsync($"/api/products/{nonExistentId}", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

---

## Testing Caching Behavior

### Pattern 1: Verify Cache Hit

```csharp
[Fact]
public async Task GetById_SecondCall_UsesCachedValue()
{
    // Arrange
    var product = new Product { Id = Guid.NewGuid(), Name = "Cached Product" };
    SeedDatabase(product);

    var mockCache = new Mock<ICacheService>();
    var service = new ProductService(DbContext, mockCache.Object, Logger);

    // First call - cache miss
    mockCache.Setup(c => c.GetAsync<Product>(It.IsAny<string>(), default))
        .ReturnsAsync(Result<Product>.Failure("Cache miss", 404));

    await service.GetByIdAsync(product.Id);

    // Setup cache hit for second call
    mockCache.Setup(c => c.GetAsync<Product>(It.IsAny<string>(), default))
        .ReturnsAsync(Result<Product>.Success(product));

    // Act - Second call
    var result = await service.GetByIdAsync(product.Id);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Data.Name.Should().Be("Cached Product");

    // Verify cache was checked
    mockCache.Verify(c => c.GetAsync<Product>(It.IsAny<string>(), default), Times.Exactly(2));
}
```

### Pattern 2: Verify Cache Invalidation

```csharp
[Fact]
public async Task Update_InvalidatesCacheForProduct()
{
    // Arrange
    var product = new Product { Id = Guid.NewGuid(), Name = "Original" };
    SeedDatabase(product);

    var mockCache = new Mock<ICacheService>();
    var service = new ProductService(DbContext, mockCache.Object, Logger);

    var request = new UpdateProductRequest { Name = "Updated" };

    // Act
    await service.UpdateAsync(product.Id, request);

    // Assert - Cache was invalidated
    mockCache.Verify(c => c.RemoveAsync(
        It.Is<string>(key => key.Contains(product.Id.ToString())), 
        default), 
        Times.Once);

    mockCache.Verify(c => c.RemoveByPrefixAsync(
        It.Is<string>(prefix => prefix.Contains("product:")), 
        default),
        Times.Once);
}
```

---

## Mock and Stub Patterns

### Pattern 1: Mocking Dependencies

```csharp
public class WalletServiceTests
{
    [Fact]
    public async Task Transfer_CallsExternalServices_InCorrectOrder()
    {
        // Arrange
        var mockTenantService = new Mock<ITenantService>();
        var mockIdentityService = new Mock<IIdentityServerServiceWrapper>();
        var mockLogger = new Mock<ILogger<WalletService>>();

        var service = new WalletService(
            DbContext,
            mockTenantService.Object,
            mockIdentityService.Object,
            mockLogger.Object);

        var tenant = new Tenant { Id = Guid.NewGuid() };
        mockTenantService.Setup(s => s.GetTenant(It.IsAny<Guid>()))
            .ReturnsAsync(tenant);

        // Act
        var request = new TransferWalletRequest { /* ... */ };
        await service.TransferAsync(request);

        // Assert - Verify call sequence
        mockTenantService.Verify(s => s.GetTenant(It.IsAny<Guid>()), Times.Once);
        mockIdentityService.Verify(s => s.IdentityCredential.Get(
            It.IsAny<Guid>(), 
            It.IsAny<bool>(), 
            It.IsAny<string[]>(), 
            It.IsAny<Guid>()), 
            Times.AtLeastOnce);
    }
}
```

### Pattern 2: Stub for External Services

```csharp
public class FakeEmailService : IEmailService
{
    public List<EmailMessage> SentEmails { get; } = new();

    public Task SendEmailAsync(EmailMessage message)
    {
        SentEmails.Add(message);
        return Task.CompletedTask;
    }
}

[Fact]
public async Task CreateProduct_SendsNotificationEmail()
{
    // Arrange
    var fakeEmailService = new FakeEmailService();
    var service = new ProductService(DbContext, MockCache.Object, Logger, fakeEmailService);

    // Act
    await service.CreateAsync(new CreateProductRequest { Name = "New Product" });

    // Assert
    fakeEmailService.SentEmails.Should().HaveCount(1);
    fakeEmailService.SentEmails[0].Subject.Should().Contain("New Product");
}
```

---

## Testing Result<T> Pattern

### Pattern 1: Success Scenarios

```csharp
[Fact]
public void Result_Success_HasCorrectProperties()
{
    // Arrange
    var product = new Product { Id = Guid.NewGuid(), Name = "Test" };

    // Act
    var result = Result<Product>.Success(product, "Operation successful");

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Data.Should().NotBeNull();
    result.Data.Should().BeSameAs(product);
    result.StatusCode.Should().Be(200);
    result.Message.Should().Be("Operation successful");
    result.Errors.Should().BeNull();
}
```

### Pattern 2: Failure Scenarios

```csharp
[Fact]
public void Result_Failure_HasCorrectProperties()
{
    // Act
    var result = Result<Product>.Failure("Operation failed", 400);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Data.Should().BeNull();
    result.StatusCode.Should().Be(400);
    result.Message.Should().Be("Operation failed");
}

[Fact]
public void Result_NotFound_Returns404()
{
    // Act
    var result = Result<Product>.NotFound("Product not found");

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.StatusCode.Should().Be(404);
    result.Message.Should().Contain("not found");
}
```

---

## Example Test Cases

### Complete Service Test Suite

```csharp
public class ProductServiceTestSuite : ServiceTestBase<ProductService>
{
    private readonly ProductService _service;

    public ProductServiceTestSuite()
    {
        _service = new ProductService(DbContext, MockCache.Object, Logger);
    }

    [Fact]
    public async Task Scenario_CreateAndRetrieveProduct_Success()
    {
        // Create
        var createRequest = new CreateProductRequest
        {
            Name = "Test Product",
            SKU = "TEST-001",
            Price = 99.99m
        };

        var createResult = await _service.CreateAsync(createRequest);
        createResult.IsSuccess.Should().BeTrue();
        var productId = createResult.Data.Id;

        // Retrieve
        var getResult = await _service.GetByIdAsync(productId);
        getResult.IsSuccess.Should().BeTrue();
        getResult.Data.Name.Should().Be("Test Product");
    }

    [Fact]
    public async Task Scenario_UpdateAndVerifyChanges_Success()
    {
        // Arrange - Create product
        var product = new Product { Id = Guid.NewGuid(), Name = "Original", Price = 50m };
        SeedDatabase(product);

        // Act - Update
        var updateRequest = new UpdateProductRequest { Name = "Updated", Price = 75m };
        var updateResult = await _service.UpdateAsync(product.Id, updateRequest);
        
        // Assert - Verify update
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Data.Name.Should().Be("Updated");

        // Verify in database
        var retrieved = await _service.GetByIdAsync(product.Id);
        retrieved.Data.Price.Should().Be(75m);
    }

    [Fact]
    public async Task Scenario_DeleteAndVerifyNotRetrievable_Success()
    {
        // Arrange
        var product = new Product { Id = Guid.NewGuid(), Name = "To Delete" };
        SeedDatabase(product);

        // Act - Delete
        var deleteResult = await _service.DeleteAsync(product.Id);
        deleteResult.IsSuccess.Should().BeTrue();

        // Assert - Cannot retrieve deleted product
        var getResult = await _service.GetByIdAsync(product.Id);
        getResult.IsSuccess.Should().BeFalse();
        getResult.StatusCode.Should().Be(404);
    }
}
```

---

## Related Documentation

- [VSA Migration Guide](../guides/vsa-migration-guide.md)
- [Result Pattern Guide](./result-pattern-guide.md)
- [Caching Strategy Guide](./caching-strategy.md)
- [Logging Standards](../../docs/standards/logging-standards.md)

---

**Last Updated**: 2025-11-20  
**Version**: 1.0  
**Author**: XFramework Development Team
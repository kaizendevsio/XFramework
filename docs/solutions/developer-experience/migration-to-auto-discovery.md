---
title: "Migration to Auto-Discovery Guide"
date: 2026-05-15
category: developer-experience
module: XFramework.SourceGenerators
problem_type: developer_experience
component: tooling
severity: medium
applies_when:
  - "Migrating manual endpoint mapping and service registration to generated auto-discovery"
tags: [migration, auto-discovery, source-generators, registration]
---

# Migration to Auto-Discovery Guide

This guide provides step-by-step instructions for migrating from manual endpoint and service registration to the automatic discovery system.

## Table of Contents

- [Overview](#overview)
- [Pre-Migration Checklist](#pre-migration-checklist)
- [Migration Steps](#migration-steps)
- [Verification](#verification)
- [Rollback Plan](#rollback-plan)
- [Common Scenarios](#common-scenarios)

## Overview

### What Changes

**Before Auto-Discovery:**
```csharp
// Program.cs
var builder = XApplication.Configure<Program>();

// Manual service registration (one per entity)
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
// ... potentially dozens more

var app = (WebApplication)builder.Build();

// Manual endpoint mapping (one per entity)
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapCustomerEndpoints();
app.MapInvoiceEndpoints();
// ... potentially dozens more

app.Run();
```

**After Auto-Discovery:**
```csharp
// Program.cs
using XFramework.Core.Extensions;

var builder = XApplication.Configure<Program>();

// Single call replaces all manual service registrations
builder.Services.AddGeneratedServices();

var app = (WebApplication)builder.Build();

// Single call replaces all manual endpoint mappings
app.MapGeneratedEndpoints();

app.Run();
```

### Migration Benefits

- ✅ **90% less code** in Program.cs
- ✅ **Zero maintenance** when adding new entities
- ✅ **No forgotten registrations** - automatic when attribute is applied
- ✅ **Consistent patterns** across all entities
- ✅ **Faster onboarding** for new developers

### Migration Risks

- ⚠️ **Breaking changes** if manual registrations had custom config
- ⚠️ **Service lifetime changes** (default is Scoped, was potentially mixed before)
- ⚠️ **Assembly filter** may need adjustment for non-standard naming

### Estimated Time

- **Small project** (1-10 entities): 15-30 minutes
- **Medium project** (10-50 entities): 30-60 minutes
- **Large project** (50+ entities): 1-2 hours

## Pre-Migration Checklist

### 1. Verify Prerequisites

Ensure you have:

- ✅ XFramework.Core with auto-discovery extensions
- ✅ All entities using `[GenerateEndpoints]` attribute
- ✅ Source generators running successfully
- ✅ Build succeeds without errors

Check generator output:
```bash
dotnet build /v:diag | grep "SourceGenerator"
```

### 2. Document Current Registrations

Create a list of all manually registered items:

```bash
# Find all manual endpoint mappings
grep -r "\.Map.*Endpoints()" --include="Program.cs"

# Find all manual service registrations
grep -r "AddScoped<I.*Service" --include="Program.cs"
grep -r "AddSingleton<I.*Service" --include="Program.cs"
grep -r "AddTransient<I.*Service" --include="Program.cs"
```

### 3. Identify Special Cases

Review your registrations for:

- **Custom lifetimes** (Singleton, Transient instead of Scoped)
- **Decorators** (services wrapped with additional behavior)
- **Conditional registration** (based on environment, config)
- **Custom authorization** (specific endpoint-level auth)
- **Custom configuration** (special setup required)

Mark these for exclusion using `[ExcludeFromAutoDiscovery]`.

### 4. Create Backup

```bash
# Create a backup branch
git checkout -b backup/pre-auto-discovery
git add .
git commit -m "Backup before auto-discovery migration"
git checkout main

# Or create a backup copy
cp src/YourProject/Program.cs src/YourProject/Program.cs.backup
```

## Migration Steps

### Step 1: Clean Compile

Ensure project builds cleanly before starting:

```bash
dotnet clean
dotnet build
```

Fix any errors before proceeding.

### Step 2: Add Using Statement

Add the auto-discovery extension namespace to `Program.cs`:

```csharp
using XFramework.Core.Extensions;
```

### Step 3: Replace Service Registrations

**Before:**
```csharp
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
// ... many more
```

**After:**
```csharp
// Replace all with single call
builder.Services.AddGeneratedServices();
```

**With Custom Lifetime (if needed):**
```csharp
// If most services were Singleton:
builder.Services.AddGeneratedServices(lifetime: ServiceLifetime.Singleton);
```

### Step 4: Handle Service Special Cases

For services with custom configuration, exclude from auto-discovery:

```csharp
// In the service interface/implementation file:
using XFramework.Core.Attributes;

[ExcludeFromAutoDiscovery("Requires decorator pattern")]
public interface IPaymentService { }

[ExcludeFromAutoDiscovery("Requires decorator pattern")]
public class PaymentService : IPaymentService { }
```

Then keep manual registration in Program.cs:

```csharp
// After auto-discovery, add manual registrations for excluded services
builder.Services.AddGeneratedServices();

// Manual registrations for excluded services
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.Decorate<IPaymentService, CachedPaymentService>();
```

### Step 5: Replace Endpoint Mappings

**Before:**
```csharp
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapCustomerEndpoints();
// ... many more
```

**After:**
```csharp
// Replace all with single call
app.MapGeneratedEndpoints();
```

### Step 6: Handle Endpoint Special Cases

For endpoints with custom configuration, exclude from auto-discovery:

```csharp
// In the endpoint file:
using XFramework.Core.Attributes;

[ExcludeFromAutoDiscovery("Requires custom admin authorization")]
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Custom configuration
        return app;
    }
}
```

Then manually map after auto-discovery:

```csharp
// After auto-discovery, add manual mappings for excluded endpoints
app.MapGeneratedEndpoints();

// Manual mappings for excluded endpoints
app.MapEndpoint<AdminEndpoints>();
```

### Step 7: Review Final Program.cs

Your `Program.cs` should now look like this:

```csharp
using XFramework.Core.Extensions;

var builder = XApplication.Configure<Program>();

// Auto-discover and register all generated services
builder.Services.AddGeneratedServices();

// Add any manually excluded services
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.Decorate<IPaymentService, CachedPaymentService>();

var app = (WebApplication)builder.Build();

// Auto-discover and map all generated endpoints
app.MapGeneratedEndpoints();

// Add any manually excluded endpoints
app.MapEndpoint<AdminEndpoints>();

app.Run();
```

### Step 8: Build and Test

```bash
# Clean and build
dotnet clean
dotnet build

# Run the application
dotnet run
```

## Verification

### Step 1: Check Build Output

Look for auto-discovery log messages:

```
[INFO] Starting auto-discovery of services. Found X service pairs to register
[INFO] Service auto-discovery completed in Xms. Registered: X, Skipped: X
[INFO] Starting auto-discovery of endpoints. Found X endpoint types to register
[INFO] Endpoint auto-discovery completed in Xms. Registered: X, Failed: X
```

### Step 2: Verify Swagger Documentation

1. Navigate to `/swagger` endpoint
2. Verify all expected endpoints are listed
3. Compare with pre-migration Swagger output

**Checklist:**
- ✅ All entity endpoints present
- ✅ HTTP methods correct (GET, POST, PUT, DELETE)
- ✅ Route paths correct
- ✅ Authorization requirements shown
- ✅ No duplicate endpoints

### Step 3: Test Endpoints

Create a test script to verify all endpoints:

```bash
#!/bin/bash
# test-endpoints.sh

BASE_URL="http://localhost:5000"
TOKEN="your-jwt-token"

# Test Product endpoints
curl -X GET "$BASE_URL/api/products" -H "Authorization: Bearer $TOKEN"
curl -X GET "$BASE_URL/api/products/123" -H "Authorization: Bearer $TOKEN"
curl -X POST "$BASE_URL/api/products" -H "Authorization: Bearer $TOKEN" -d '{"name":"Test"}'

# Test Order endpoints
curl -X GET "$BASE_URL/api/orders" -H "Authorization: Bearer $TOKEN"
# ... more tests
```

### Step 4: Verify Service Injection

Check that services are properly injected:

```csharp
// In a test endpoint or controller
public class TestController
{
    private readonly IProductService _productService;
    private readonly IOrderService _orderService;
    
    public TestController(
        IProductService productService,
        IOrderService orderService)
    {
        _productService = productService; // Should resolve successfully
        _orderService = orderService;     // Should resolve successfully
    }
}
```

### Step 5: Performance Check

Monitor startup time:

```bash
# Check logs for startup duration
# Should see auto-discovery complete in <100ms

[INFO] Service auto-discovery completed in 45ms
[INFO] Endpoint auto-discovery completed in 67ms
```

### Step 6: Regression Testing

Run your full test suite:

```bash
dotnet test
```

Ensure:
- ✅ All tests pass
- ✅ No new failures introduced
- ✅ Integration tests succeed
- ✅ E2E tests succeed

## Rollback Plan

If issues occur, you can quickly rollback:

### Option 1: Git Revert

```bash
git checkout backup/pre-auto-discovery
# Or
git revert HEAD
```

### Option 2: Restore Backup File

```bash
cp src/YourProject/Program.cs.backup src/YourProject/Program.cs
```

### Option 3: Disable Auto-Discovery

Keep auto-discovery code but revert to manual temporarily:

```csharp
// Comment out auto-discovery
// builder.Services.AddGeneratedServices();
// app.MapGeneratedEndpoints();

// Restore manual registrations
builder.Services.AddScoped<IProductService, ProductService>();
// ... etc
```

## Common Scenarios

### Scenario 1: Mixed Service Lifetimes

**Problem**: Some services were Singleton, some Scoped

**Before:**
```csharp
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
```

**Solution**: Exclude Singleton services, auto-discover Scoped

```csharp
// Exclude Singleton services
[ExcludeFromAutoDiscovery("Requires Singleton lifetime")]
public class CacheService : ICacheService { }

// In Program.cs:
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddGeneratedServices(); // Discovers remaining Scoped services
```

### Scenario 2: Decorated Services

**Problem**: Services wrapped with decorators

**Before:**
```csharp
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.Decorate<IProductService, CachedProductService>();
```

**Solution**: Exclude from auto-discovery, keep manual

```csharp
[ExcludeFromAutoDiscovery("Decorated with caching")]
public class ProductService : IProductService { }

// In Program.cs:
builder.Services.AddGeneratedServices();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.Decorate<IProductService, CachedProductService>();
```

### Scenario 3: Conditional Registration

**Problem**: Services registered based on environment

**Before:**
```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IEmailService, MockEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, EmailService>();
}
```

**Solution**: Keep conditional logic, exclude from auto-discovery

```csharp
[ExcludeFromAutoDiscovery("Conditional registration")]
public class EmailService : IEmailService { }

[ExcludeFromAutoDiscovery("Conditional registration")]
public class MockEmailService : IEmailService { }

// In Program.cs:
builder.Services.AddGeneratedServices();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IEmailService, MockEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, EmailService>();
}
```

### Scenario 4: Custom Endpoint Authorization

**Problem**: Endpoint requires special authorization setup

**Before:**
```csharp
app.MapGet("/api/admin/users", ...)
    .RequireAuthorization("SuperAdmin");
```

**Solution**: Exclude endpoint, keep manual mapping

```csharp
[ExcludeFromAutoDiscovery("Requires SuperAdmin authorization")]
public static class AdminUserEndpoints { }

// In Program.cs:
app.MapGeneratedEndpoints();
app.MapEndpoint<AdminUserEndpoints>();
```

### Scenario 5: Non-Standard Naming

**Problem**: Service doesn't follow I*Service → *Service pattern

**Before:**
```csharp
public interface IProductRepo { }
public class ProductRepository : IProductRepo { }
builder.Services.AddScoped<IProductRepo, ProductRepository>();
```

**Solution**: Won't be auto-discovered, keep manual (by design)

```csharp
// No exclusion needed - won't be discovered due to naming
// Just keep manual registration
builder.Services.AddGeneratedServices();
builder.Services.AddScoped<IProductRepo, ProductRepository>();
```

### Scenario 6: Module-Based Architecture

**Problem**: Project organized into multiple modules

**Solution**: Use module-specific discovery

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

// In Program.cs:
builder.Services.AddInventarioServices();
builder.Services.AddWalletsServices();
builder.Services.AddMessagingServices();

app.MapInventarioEndpoints();
app.MapWalletsEndpoints();
app.MapMessagingEndpoints();
```

### Scenario 7: Gradual Migration

**Problem**: Large codebase, want to migrate incrementally

**Solution**: Hybrid approach with filters

```csharp
// Phase 1: Auto-discover only Inventario module
builder.Services.AddGeneratedServices(
    asm => asm.FullName?.Contains("Inventario") == true);

// Keep manual for others (temporarily)
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IMessageService, MessageService>();

// Same for endpoints
app.MapGeneratedEndpoints(
    asm => asm.FullName?.Contains("Inventario") == true);

app.MapWalletEndpoints();
app.MapMessageEndpoints();

// Phase 2: Expand to Wallets
// Phase 3: Expand to all modules
```

## Next Steps

After successful migration:

1. **Document Exclusions**: Create a document listing all excluded types and reasons
2. **Update Onboarding**: Update developer documentation with auto-discovery info
3. **Monitor Performance**: Track startup time over next few releases
4. **Remove Backups**: Clean up backup files/branches after stable period

## Getting Help

If you encounter issues during migration:

1. Check [Auto-Discovery Guide](../tooling-decisions/generated-endpoint-auto-discovery.md)
2. Check [Troubleshooting Section](../tooling-decisions/generated-endpoint-auto-discovery.md#troubleshooting)
3. Review logs for discovery errors
4. Enable diagnostic logging:
   ```csharp
   builder.Logging.AddConsole();
   builder.Logging.SetMinimumLevel(LogLevel.Debug);
   ```

---

**Version**: 1.0  
**Last Updated**: 2025-11-20  
**Phase**: 5.4 - Auto-Discovery & Registration

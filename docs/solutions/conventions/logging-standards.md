---
title: "XFramework Logging Standards"
date: 2026-05-15
category: conventions
module: XFramework.Core
problem_type: convention
component: tooling
severity: medium
applies_when:
  - "Writing or reviewing structured logging, event IDs, log levels, correlation IDs, masking, or LoggerMessage usage"
tags: [logging, structured-logging, loggermessage, event-id, correlation]
---

# XFramework Logging Standards

**Status:** Current standards for the ZLogger + Microsoft.Extensions.Logging pipeline. Serilog references in older migration documents are historical only.

## Overview

XFramework uses **LoggerMessage source generators** for high-performance, zero-allocation structured logging and **ZLogger** as the active provider pipeline for console and Seq output. This document defines standards, conventions, and best practices for logging across all services.

## Benefits of LoggerMessage Source Generators

- **3-5x Faster**: Compile-time code generation eliminates runtime overhead
- **Zero Allocations**: No string interpolation or boxing
- **Type Safety**: Compile-time parameter validation
- **Consistent Structure**: Standardized log messages across the application
- **Better Analysis**: Structured data enables powerful log analytics

## EventId Ranges

All log messages are categorized by EventId ranges for easy filtering and analysis:

| Range | Category | Level | Examples |
|-------|----------|-------|----------|
| 1000-1999 | CRUD Operations | Information | Entity creating, created, updating, updated, deleting, deleted |
| 2000-2999 | Cache Operations | Debug/Information | Cache hit, miss, set, invalidated, cleared |
| 3000-3999 | Performance Metrics | Information/Warning | Operation completed, slow operation, high memory usage |
| 4000-4999 | Security Events | Information/Warning | User authenticated, login failed, unauthorized access, token validation |
| 5000-5999 | Errors/Exceptions | Error/Critical | Operation failed, validation failed, database error, critical error |
| 6000-6999 | Wallet/Financial | Information/Warning | Balance increment, decrement, transfer, insufficient balance |
| 7000-7999 | Messaging | Information/Warning | Message sent, delivered, delivery failed, SMS sent |
| 8000-8999 | Integration/External | Information/Warning/Error | API calls, retries, blockchain transactions, WebSocket events |

## Log Levels

### Trace
**When**: Very detailed diagnostic information for deep debugging.
**Example**: Individual loop iterations, detailed state transitions.
```csharp
// Not commonly used in LogMessages.cs - use Debug instead
```

### Debug
**When**: Development-time diagnostics, cache operations.
**Example**: Cache hits/misses, configuration values loaded.
```csharp
_logger.CacheHit("products:12345");
_logger.CacheMiss("users:67890");
```

### Information
**When**: General application flow, successful operations.
**Example**: CRUD operations, successful authentication, batch operations.
```csharp
_logger.EntityCreated("Product", productId);
_logger.UserAuthenticated(userId, ipAddress);
_logger.WalletIncremented(walletId, amount, currency, newBalance);
```

### Warning
**When**: Unexpected but handled situations, business rule violations.
**Example**: Validation failures, insufficient balance, slow operations.
```csharp
_logger.EntityNotFound("Product", productId);
_logger.InsufficientBalance(walletId, requiredAmount, availableBalance);
_logger.BusinessRuleViolation("Transfer", "Amount exceeds daily limit");
```

### Error
**When**: Errors requiring attention, operation failures.
**Example**: Database errors, external service failures, unhandled exceptions.
```csharp
_logger.OperationFailed("CreateProduct", "Product", productId, ex.Message, ex);
_logger.DatabaseOperationFailed("SaveChanges", ex);
_logger.ExternalServiceFailed("PaymentGateway", "/api/charge", ex);
```

### Critical
**When**: System failures requiring immediate action.
**Example**: Application startup failures, data corruption, security breaches.
```csharp
_logger.CriticalError("Database", "Connection pool exhausted", ex);
```

## Using Structured Logging

### Step 1: Import the Namespace

```csharp
using XFramework.Core.Loggers;
```

### Step 2: Inject ILogger

```csharp
private readonly ILogger<MyService> _logger;

public MyService(ILogger<MyService> logger)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

### Step 3: Use Extension Methods

```csharp
// CRUD Operations
_logger.EntityCreating("Product", productId, tenantId);
_logger.EntityCreated("Product", productId);
_logger.EntityUpdating("Product", productId);
_logger.EntityUpdated("Product", productId);

// Errors
_logger.OperationFailed("CreateProduct", "Product", productId, ex.Message, ex);
_logger.ValidationFailed("Product", "Price must be positive");

// Performance
_logger.OperationCompleted("BatchImport", stopwatch.ElapsedMilliseconds);
```

## Common Patterns

### CRUD Operations

```csharp
public async Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken ct)
{
    var productId = Guid.NewGuid();
    _logger.EntityCreating("Product", productId, request.TenantId);
    
    try
    {
        // Create logic...
        
        _logger.EntityCreated("Product", productId);
        return Result<Product>.Success(product);
    }
    catch (Exception ex)
    {
        _logger.OperationFailed("Create", "Product", productId, ex.Message, ex);
        return Result<Product>.Failure("An error occurred", 500);
    }
}
```

### Financial Operations

```csharp
public async Task<Result> IncrementBalanceAsync(IncrementRequest request, CancellationToken ct)
{
    try
    {
        // Increment logic...
        
        _logger.WalletIncremented(wallet.Id, request.Amount, "USD", wallet.Balance);
        _logger.TransactionCreated(transaction.Id, wallet.Id, "Credit", request.Amount);
        
        return Result.Success();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        _logger.ConcurrencyConflict("Wallet", wallet.Id);
        return Result.Failure("Concurrency conflict", 409);
    }
}
```

### Security Operations

```csharp
public async Task<Result<AuthResponse>> AuthenticateAsync(AuthRequest request, CancellationToken ct)
{
    var credential = await ValidateCredentials(request);
    
    if (credential == null)
    {
        _logger.LoginFailed(request.UserName, request.IpAddress);
        return Result<AuthResponse>.Failure("Invalid credentials", 401);
    }
    
    _logger.UserAuthenticated(credential.Id, request.IpAddress);
    return Result<AuthResponse>.Success(response);
}
```

## Correlation IDs

Every HTTP request automatically gets a correlation ID via `CorrelationIdMiddleware`. This ID is:

- Generated automatically or extracted from `X-Correlation-ID` request header
- Added to log entries through Microsoft.Extensions.Logging scopes
- Returned in the `X-Correlation-ID` response header
- Useful for tracing requests across services

**Accessing in Code**:
```csharp
var correlationId = HttpContext.GetCorrelationId();
```

## Adding New Log Messages

### 1. Determine the Appropriate EventId Range

Choose the correct range based on the log category (CRUD, Security, Performance, etc.)

### 2. Add to LogMessages.cs

```csharp
[LoggerMessage(
    EventId = 6008,  // Choose next available ID in range
    Level = LogLevel.Information,
    Message = "Wallet {WalletId} locked for maintenance until {UnlockTime}")]
public static partial void WalletLocked(
    this ILogger logger, Guid walletId, DateTime unlockTime);
```

### 3. Use in Your Service

```csharp
_logger.WalletLocked(walletId, DateTime.UtcNow.AddHours(2));
```

## Data Masking & Security

### Never Log Sensitive Data

❌ **DON'T**:
```csharp
_logger.LogInformation("User {UserId} logged in with password {Password}", userId, password);
```

✅ **DO**:
```csharp
_logger.UserAuthenticated(userId, ipAddress);
```

### Sensitive Data Types to Mask

- Passwords
- Tokens (JWT, API keys, session tokens)
- Credit card numbers
- Social security numbers
- Full email addresses (mask: `u***r@example.com`)
- Full phone numbers (mask: `+1***5678`)

### Use Masking Functions

```csharp
private static string MaskEmail(string email)
{
    var parts = email.Split('@');
    return parts.Length == 2 
        ? $"{parts[0][0]}***{parts[0][^1]}@{parts[1]}" 
        : email;
}
```

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System": "Warning",
      "XFramework": "Information"
    }
  },
  "Seq": {
    "Url": "http://localhost:5341"
  }
}
```

Services should call `builder.Logging.AddXFrameworkLogging(builder.Configuration)`. The extension clears competing providers, configures ZLogger console output, and sends CLEF events to Seq when `Seq:Url` is present.

### Environment-Specific Settings

**Development** (`appsettings.Development.json`):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "XFramework": "Debug"
    }
  }
}
```

**Production** (`appsettings.json`):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "XFramework": "Information"
    }
  }
}
```

## Performance Considerations

### LoggerMessage Benefits

- **Compile-time generation**: No runtime reflection or string allocation
- **Strongly typed**: Type-safe parameters
- **Efficient**: Minimal overhead per log call

### Cost Comparison

```csharp
// ❌ Traditional (slow, allocates strings)
_logger.LogInformation($"Creating {entityType} with ID {entityId}");

// ✅ LoggerMessage (fast, zero allocation)
_logger.EntityCreating(entityType, entityId, tenantId);
```

### When to Log

- ✅ **DO**: Log at key decision points, state changes, errors
- ✅ **DO**: Log performance metrics for slow operations
- ❌ **DON'T**: Log inside tight loops
- ❌ **DON'T**: Log on every iteration of batch operations (log summary instead)

## Troubleshooting

### Logs Not Appearing

1. Check `MinimumLevel` in `appsettings.json`
2. Verify namespace override settings
3. Ensure `CorrelationIdMiddleware` is registered early in pipeline
4. Ensure `AddXFrameworkLogging()` is called and `Seq:Url` is set when expecting Seq events

### Missing Correlation IDs

1. Verify `CorrelationIdMiddleware` is registered: `app.UseCorrelationId();`
2. Ensure middleware is before other middleware that logs
3. Check logging scopes are included by the active ZLogger providers

### Performance Issues

1. Reduce log level in production (Warning/Error only)
2. Use asynchronous sinks for file/database logging
3. Implement log sampling for high-volume events
4. Filter noisy logs (EF Core query logs)

## Best Practices Summary

1. ✅ Always use `LoggerMessage` extension methods from `LogMessages.cs`
2. ✅ Use appropriate log levels (Debug for cache, Information for operations, Error for failures)
3. ✅ Include correlation IDs automatically via middleware
4. ✅ Mask sensitive data before logging
5. ✅ Use structured parameters, not string interpolation
6. ✅ Log at key decision points, not every line
7. ✅ Include context: entity types, IDs, amounts, user IDs
8. ✅ Add performance metrics for operations over 100ms
9. ❌ Never log passwords, tokens, or sensitive PII
10. ❌ Don't log inside loops (use summary logs instead)

## References

- [Microsoft LoggerMessage Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)
- [ZLogger](https://github.com/Cysharp/ZLogger)
- [Structured Logging Guide](https://stackify.com/what-is-structured-logging-and-why-developers-need-it/)

---

**Last Updated**: 2026-05-21
**Version**: 1.1
**Author**: XFramework Development Team

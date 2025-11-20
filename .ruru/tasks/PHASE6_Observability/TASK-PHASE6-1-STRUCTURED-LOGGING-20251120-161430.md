+++
# --- Task Metadata ---
id = "TASK-PHASE6-1-STRUCTURED-LOGGING-20251120-161430"
title = "Phase 6.1: Structured Logging with LoggerMessage Source Generators"
status = "🟡 To Do"
type = "🌟 Feature"
priority = "high"
assigned_to = "util-senior-dev"
coordinator = "TASK-CMD-20251119-192100"
created_date = "2025-11-20T16:14:30Z"
updated_date = "2025-11-20T16:14:30Z"
tags = ["phase-6", "observability", "logging", "structured-logging", "source-generators", "performance"]
related_docs = [
    "XFramework-Development-Roadmap.md",
    "src/Kernel/XFramework.Core/Services/ProductService.cs",
    "src/Modules/XFramework.Wallets/Wallets.Core/Services/WalletService.cs"
]
+++

# Task: Structured Logging with LoggerMessage Source Generators

## 📋 Overview

**Goal**: Implement high-performance structured logging using `LoggerMessage` source generators across all services, replacing ad-hoc `ILogger` calls with compile-time generated, zero-allocation logging methods.

**Phase**: Phase 6.1 - Structured Logging
**Complexity**: Moderate
**Estimated Effort**: 4-6 hours

## 🎯 Objectives

1. Create centralized logging infrastructure using `LoggerMessage` source generators
2. Define standard log message templates for common operations (CRUD, errors, performance)
3. Apply structured logging to all services (7 backend modules + Inventario)
4. Configure log levels per environment
5. Add correlation IDs for request tracing
6. Document logging standards and conventions

## 📦 Context

**Current State**:
- Services use `ILogger<T>` with string interpolation
- Inconsistent log message formats
- Performance overhead from string allocation
- No standardized log structure

**Desired State**:
- Zero-allocation logging with `LoggerMessage` source generators
- Consistent log message formats across all services
- Structured data for log analysis (JSON format)
- Correlation IDs for distributed tracing
- Performance improvement: 3-5x faster logging

**Pattern**:
```csharp
// Before (string allocation, runtime overhead):
_logger.LogInformation($"Creating product {productName} for tenant {tenantId}");

// After (compile-time generated, zero allocation):
_logger.ProductCreating(productName, tenantId);
```

## ✅ Acceptance Criteria

### Core Functionality
- [ ] `LoggerMessage` source generators configured
- [ ] Centralized logging class created (e.g., `LogMessages.cs`)
- [ ] Standard templates for CRUD operations
- [ ] Standard templates for errors/exceptions
- [ ] Standard templates for performance metrics
- [ ] All services updated to use new logging
- [ ] Correlation IDs added to all log entries

### Configuration
- [ ] Log levels per environment (appsettings.json)
- [ ] Console output format configured
- [ ] Serilog enrichers added (machine, environment, correlation)
- [ ] Log filtering rules configured

### Documentation
- [ ] Logging standards document created
- [ ] Examples for adding new log messages
- [ ] Guidelines for log levels (Debug, Info, Warning, Error)

## 📝 Detailed Checklist

### Section 1: Infrastructure Setup (45 min)

#### 1.1 Create Logging Infrastructure
- [✅] Create `src/Kernel/XFramework.Core/Logging/LogMessages.cs`
- [✅] Add `LoggerMessage` attribute examples
- [✅] Create standard templates for:
  - CRUD operations (Creating, Created, Getting, Got, Updating, Updated, Deleting, Deleted)
  - Errors (OperationFailed, ValidationFailed, NotFound)
  - Performance (OperationCompleted with duration)
  - Cache operations (CacheHit, CacheMiss, CacheInvalidated)

#### 1.2 Define Log Message Categories
```csharp
// CRUD Operations
[LoggerMessage(
    EventId = 1001,
    Level = LogLevel.Information,
    Message = "Creating {EntityType} with ID {EntityId} for tenant {TenantId}")]
public static partial void EntityCreating(
    this ILogger logger, string entityType, Guid entityId, Guid? tenantId);

// Errors
[LoggerMessage(
    EventId = 5001,
    Level = LogLevel.Error,
    Message = "Operation failed: {Operation} for {EntityType} {EntityId}. Error: {ErrorMessage}")]
public static partial void OperationFailed(
    this ILogger logger, string operation, string entityType, 
    Guid entityId, string errorMessage, Exception? exception = null);
```

#### 1.3 Add Correlation ID Support
- [✅] Create `CorrelationIdMiddleware.cs` in `XFramework.Core/Middlewares/`
- [✅] Generate correlation ID per request (`Guid.NewGuid()`)
- [✅] Store in `HttpContext.Items["CorrelationId"]`
- [✅] Add to all log entries via Serilog enricher
- [✅] Include in API responses via header (`X-Correlation-ID`)

### Section 2: Apply to Core Services (60 min)

#### 2.1 Update Inventario ProductService
- [✅] Replace all `_logger.LogInformation()` with structured methods
- [✅] Add correlation ID to log context
- [✅] Use EventIds for categorization
- [✅] Include performance metrics (operation duration)
- [ ] Build and test

#### 2.2 Update Wallets WalletService
- [✅] Replace logging in WalletService (1,168 lines)
- [✅] Replace logging in BatchWalletService (726 lines)
- [✅] Add financial operation logging (increment, decrement, transfer)
- [✅] Include transaction amounts and balance changes
- [ ] Build and test

#### 2.3 Update IdentityServer AuthService
- [✅] Replace logging in AuthService (963 lines)
- [✅] Add security event logging (login attempts, failures)
- [✅] Include user IDs and IP addresses
- [✅] Mask sensitive data (passwords, tokens)
- [ ] Build and test

### Section 3: Apply to Remaining Modules (60 min)

#### 3.1 StreamFlow Module
- [ ] Update StreamFlowService logging (453 lines)
- [ ] Add connection event logging
- [ ] Include client/channel identifiers
- [ ] Build and test

#### 3.2 SmsGateway Module
- [ ] Update SmsService logging (228 lines)
- [ ] Add message tracking logs
- [ ] Include recipient info and status
- [ ] Build and test

#### 3.3 Messaging Module
- [ ] Update MessagingService logging (155 lines)
- [ ] Add direct message logs
- [ ] Build and test

#### 3.4 Community Module
- [ ] Update CommunityService logging (245 lines)
- [ ] Add identity operation logs
- [ ] Build and test

#### 3.5 Coins Module
- [ ] Update BlockchainService logging (64 lines)
- [ ] Add transaction logs
- [ ] Build and test

### Section 4: Configuration (45 min)

#### 4.1 Configure Serilog
- [ ] Update appsettings.json with structured logging config
- [ ] Add enrichers: Machine, Environment, CorrelationId
- [ ] Configure output format (JSON for production, console for dev)
- [ ] Set log levels per namespace:
  - Microsoft.*: Warning
  - System.*: Warning
  - XFramework.*: Information (Debug in dev)

#### 4.2 Configure Log Levels per Environment
- [ ] appsettings.Development.json: Debug level for XFramework
- [ ] appsettings.Staging.json: Information level
- [ ] appsettings.json (Production): Warning level (Information for critical paths)

#### 4.3 Add Log Filtering
- [ ] Filter noisy logs (EF Core query logs in production)
- [ ] Keep critical security logs always
- [ ] Configure log retention policies

### Section 5: Middleware Integration (30 min)

#### 5.1 Register CorrelationIdMiddleware
- [ ] Add to `XApplication.cs` middleware pipeline
- [ ] Ensure it's early in the pipeline (before logging)
- [ ] Add to all API projects' Program.cs

#### 5.2 Update Health Check Endpoints
- [ ] Add structured logging to health check failures
- [ ] Include component name and failure reason

### Section 6: Documentation (45 min)

#### 6.1 Create Logging Standards Document
- [ ] Create `docs/standards/logging-standards.md`
- [ ] Document log levels and when to use them:
  - Trace: Very detailed, for deep debugging
  - Debug: Development diagnostics
  - Information: General flow (CRUD operations)
  - Warning: Unexpected but handled situations
  - Error: Errors requiring attention
  - Critical: System failures requiring immediate action
- [ ] Document EventId ranges by category:
  - 1000-1999: CRUD operations
  - 2000-2999: Cache operations
  - 3000-3999: Performance metrics
  - 4000-4999: Security events
  - 5000-5999: Errors/Exceptions

#### 6.2 Create Examples
- [ ] Provide examples for each log level
- [ ] Show how to add new log messages
- [ ] Document correlation ID usage

#### 6.3 Update Developer Guide
- [ ] Add logging section to onboarding guide
- [ ] Link to logging standards
- [ ] Provide troubleshooting tips

### Section 7: Testing & Verification (30 min)

#### 7.1 Build All Modules
- [ ] Build each module (should succeed)
- [ ] Verify no compilation errors
- [ ] Check for logging-related warnings

#### 7.2 Runtime Testing
- [ ] Run Inventario.Api
- [ ] Verify structured logs appear in console
- [ ] Verify correlation IDs present
- [ ] Test an API call, check logs contain structured data
- [ ] Verify log levels work per environment

#### 7.3 Performance Verification
- [ ] Compare logging overhead before/after
- [ ] Expected: 3-5x faster with LoggerMessage
- [ ] Verify zero allocations via profiling (optional)

### Section 8: Finalization (15 min)

#### 8.1 Code Review
- [ ] Review all logging changes for consistency
- [ ] Ensure no sensitive data in logs (passwords, tokens)
- [ ] Verify EventIds are unique and follow ranges

#### 8.2 Update Task Status
- [ ] Mark all checklist items complete
- [ ] Update status to 🟢 Done
- [ ] Log completion in session log

## 🎨 Implementation Examples

### LogMessages.cs Pattern
```csharp
using Microsoft.Extensions.Logging;

namespace XFramework.Core.Logging;

public static partial class LogMessages
{
    // CRUD Operations (1000-1999)
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Creating {EntityType} with ID {EntityId} for tenant {TenantId}")]
    public static partial void EntityCreating(
        this ILogger logger, string entityType, Guid entityId, Guid? tenantId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Successfully created {EntityType} with ID {EntityId}")]
    public static partial void EntityCreated(
        this ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Retrieving {EntityType} with ID {EntityId}")]
    public static partial void EntityRetrieving(
        this ILogger logger, string entityType, Guid entityId);

    // Cache Operations (2000-2999)
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Cache hit for key {CacheKey}")]
    public static partial void CacheHit(
        this ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Cache miss for key {CacheKey}")]
    public static partial void CacheMiss(
        this ILogger logger, string cacheKey);

    // Performance (3000-3999)
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Operation {Operation} completed in {DurationMs}ms")]
    public static partial void OperationCompleted(
        this ILogger logger, string operation, long durationMs);

    // Security Events (4000-4999)
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "Failed login attempt for user {UserName} from IP {IpAddress}")]
    public static partial void LoginFailed(
        this ILogger logger, string userName, string ipAddress);

    // Errors (5000-5999)
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "Operation {Operation} failed for {EntityType} {EntityId}: {ErrorMessage}")]
    public static partial void OperationFailed(
        this ILogger logger, string operation, string entityType, 
        Guid entityId, string errorMessage, Exception? exception = null);
}
```

### Service Usage
```csharp
public class ProductService : IProductService
{
    private readonly ILogger<ProductService> _logger;
    
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
            return Result<Product>.Failure(ex.Message);
        }
    }
}
```

### CorrelationIdMiddleware.cs
```csharp
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

### Serilog Configuration (appsettings.json)
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning",
        "XFramework": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithEnvironmentName"]
  }
}
```

## 🚧 Potential Challenges

1. **Large Number of Log Statements**: Many services have extensive logging
   - **Solution**: Focus on key operations first, iterate through modules
2. **Sensitive Data**: Logs might contain passwords, tokens, PII
   - **Solution**: Use data masking, exclude sensitive fields from log messages
3. **Performance Impact**: More logs = more overhead
   - **Solution**: LoggerMessage is zero-allocation, but configure levels appropriately
4. **Correlation ID Propagation**: Across async boundaries
   - **Solution**: Use `AsyncLocal<T>` or Serilog's `LogContext.PushProperty`

## 📚 Reference Materials

- **LoggerMessage Source Generators**: https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator
- **Serilog Configuration**: https://github.com/serilog/serilog/wiki/Configuration-Basics
- **Structured Logging Best Practices**: https://stackify.com/what-is-structured-logging-and-why-developers-need-it/

## 🔗 Dependencies

**Blocked By**: None (can start immediately)

**Blocks**: Phase 6.2 (OpenTelemetry - will integrate with structured logs)

**Related**:
- Phase 6.3: Health Checks (will use structured logging)
- Phase 7.1: Performance Testing (will analyze logs)

## 📊 Success Metrics

- [ ] All services use LoggerMessage source generators
- [ ] Zero ad-hoc string interpolation in logs
- [ ] Correlation IDs present in all log entries
- [ ] 3-5x faster logging performance
- [ ] Consistent log structure across all modules
- [ ] Sensitive data properly masked

## 📝 Notes

**Key Design Decisions**:
1. Use EventId ranges to categorize logs (1000-1999: CRUD, 2000-2999: Cache, etc.)
2. Always include correlation ID for distributed tracing
3. Mask sensitive data at the source (before logging)
4. Configure log levels per environment (Debug in dev, Warning in prod)

**Performance Target**: Logging overhead should be <5% of total request time.

**Security**: Never log passwords, tokens, credit card numbers, or other sensitive data.
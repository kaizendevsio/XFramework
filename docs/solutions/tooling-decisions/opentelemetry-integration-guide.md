---
title: "OpenTelemetry Integration Guide for XFramework"
date: 2026-05-15
category: tooling-decisions
module: XFramework.Core
problem_type: tooling_decision
component: tooling
severity: medium
applies_when:
  - "Adding tracing, metrics, log correlation, exporters, or activity sources to XFramework services"
tags: [opentelemetry, observability, tracing, metrics, logs]
---

# OpenTelemetry Integration Guide for XFramework

## Overview

XFramework implements comprehensive observability using OpenTelemetry (OTel) for distributed tracing and metrics collection. This guide covers the setup, configuration, and usage of OpenTelemetry across all microservices.

## Architecture

### Components

1. **Distributed Tracing**: Tracks requests across service boundaries
2. **Metrics Collection**: Records custom business metrics and runtime metrics
3. **Log Correlation**: Integrates TraceId/SpanId into structured logs
4. **Automatic Instrumentation**: ASP.NET Core, Entity Framework Core, HttpClient, Redis

### Integration with Structured Logging

OpenTelemetry's TraceId and SpanId are automatically enriched into Serilog logs using `Serilog.Enrichers.Span`, enabling correlation between logs and traces.

## Installation

### NuGet Packages

Each API module requires the following packages:

```xml
<!-- Core OpenTelemetry -->
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />

<!-- Instrumentation -->
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.12" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.StackExchangeRedis" Version="1.9.0-beta.1" />

<!-- Exporters -->
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />

<!-- Serilog Integration -->
<PackageReference Include="Serilog.Enrichers.Span" Version="3.1.0" />
```

### XFramework.Core Dependencies

The core library (`XFramework.Core`) includes these packages, so API modules only need to add them via project reference.

## Configuration

### Program.cs Setup

Add OpenTelemetry to each API module's `Program.cs`:

```csharp
using XFramework.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Install OpenTelemetry with automatic instrumentation
builder.Services.InstallOpenTelemetry(
    builder.Configuration, 
    "XFramework.YourModule.Api",  // Service name
    "1.0.0"                         // Service version (optional)
);

var app = builder.Build();
// ... rest of configuration
```

### appsettings.json Configuration

#### Production Configuration (`appsettings.json`)

```json
{
  "OpenTelemetry": {
    "Sampling": {
      "Probability": 0.1
    },
    "Exporters": {
      "Console": {
        "Enabled": false
      },
      "OTLP": {
        "Enabled": true,
        "Endpoint": "http://your-otel-collector:4317"
      }
    }
  }
}
```

#### Development Configuration (`appsettings.Development.json`)

```json
{
  "OpenTelemetry": {
    "Sampling": {
      "Probability": 1.0
    },
    "Exporters": {
      "Console": {
        "Enabled": true
      },
      "OTLP": {
        "Enabled": false
      }
    }
  }
}
```

### Serilog Configuration for Trace Correlation

Update `appsettings.json` to include TraceId and SpanId in logs:

```json
{
  "Serilog": {
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId", "WithSpan"],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j} | TraceId:{TraceId} SpanId:{SpanId}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

## Custom Instrumentation

### Activity Sources

XFramework provides domain-specific activity sources in [`ActivitySources.cs`](../../../src/Kernel/XFramework.Core/Observability/ActivitySources.cs):

```csharp
public static class ActivitySources
{
    public static readonly ActivitySource Product = new("XFramework.Product");
    public static readonly ActivitySource Wallet = new("XFramework.Wallet");
    public static readonly ActivitySource Auth = new("XFramework.Auth");
    public static readonly ActivitySource StreamFlow = new("XFramework.StreamFlow");
    public static readonly ActivitySource Sms = new("XFramework.Sms");
    public static readonly ActivitySource Messaging = new("XFramework.Messaging");
    public static readonly ActivitySource Community = new("XFramework.Community");
    public static readonly ActivitySource Blockchain = new("XFramework.Blockchain");
    public static readonly ActivitySource Payment = new("XFramework.Payment");
    public static readonly ActivitySource Infrastructure = new("XFramework.Infrastructure");
}
```

### Creating Custom Spans

#### Basic Span Creation

```csharp
using System.Diagnostics;
using XFramework.Core.Observability;

public async Task<Result<Product>> CreateAsync(CreateProductRequest request)
{
    using var activity = ActivitySources.Product.StartActivity("Product.Create");
    activity?.SetTag("product.name", request.Name);
    activity?.SetTag("product.price", request.Price);
    
    try
    {
        // Business logic here
        var product = await _repository.CreateAsync(request);
        
        activity?.SetStatus(ActivityStatusCode.Ok);
        return Result<Product>.Success(product);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("exception.type", ex.GetType().FullName);
        activity?.SetTag("exception.message", ex.Message);
        throw;
    }
}
```

#### Span with Metrics

```csharp
using System.Diagnostics;
using XFramework.Core.Observability;

public async Task<Result> IncrementBalanceAsync(IncrementWalletRequest request)
{
    using var activity = ActivitySources.Wallet.StartActivity("Wallet.IncrementBalance");
    activity?.SetTag("wallet.id", request.WalletId);
    activity?.SetTag("wallet.amount", request.TotalAmount);
    
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        // Business logic
        await ProcessIncrement(request);
        
        stopwatch.Stop();
        
        // Record metrics
        XFrameworkMetrics.WalletIncrements.Add(1, 
            new KeyValuePair<string, object?>("tenant.id", tenantId.ToString()));
        XFrameworkMetrics.WalletOperationDuration.Record(stopwatch.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("operation", "increment"),
            new KeyValuePair<string, object?>("result", "success"));
        
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("operation.duration_ms", stopwatch.ElapsedMilliseconds);
        
        return Result.Success();
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        
        XFrameworkMetrics.WalletOperationDuration.Record(stopwatch.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("operation", "increment"),
            new KeyValuePair<string, object?>("result", "error"));
        
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("exception.type", ex.GetType().FullName);
        activity?.SetTag("exception.message", ex.Message);
        
        return Result.Failure("Operation failed");
    }
}
```

### Custom Metrics

XFramework provides predefined metrics in [`XFrameworkMetrics.cs`](../../../src/Kernel/XFramework.Core/Observability/XFrameworkMetrics.cs):

#### Counters (Event Counts)

```csharp
// Products
XFrameworkMetrics.ProductsCreated.Add(1, 
    new KeyValuePair<string, object?>("category_id", categoryId.ToString()));

// Wallets
XFrameworkMetrics.WalletIncrements.Add(1,
    new KeyValuePair<string, object?>("tenant.id", tenantId.ToString()));

// Authentication
XFrameworkMetrics.AuthenticationAttempts.Add(1,
    new KeyValuePair<string, object?>("result", "success"));
```

#### Histograms (Durations)

```csharp
// Product operations
XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds,
    new KeyValuePair<string, object?>("result", "success"));

// Wallet operations
XFrameworkMetrics.WalletOperationDuration.Record(duration,
    new KeyValuePair<string, object?>("operation", "increment"));

// Transaction amounts
XFrameworkMetrics.WalletTransactionAmount.Record(request.TotalAmount,
    new KeyValuePair<string, object?>("operation", "increment"));
```

## Exporter Configuration

### Console Exporter (Development)

Useful for local development and debugging. Traces are printed to the console.

**Configuration:**
```json
{
  "OpenTelemetry": {
    "Exporters": {
      "Console": {
        "Enabled": true
      }
    }
  }
}
```

### OTLP Exporter (Production)

OpenTelemetry Protocol exporter for sending traces to collectors like Jaeger, Zipkin, or commercial APM solutions.

**Configuration:**
```json
{
  "OpenTelemetry": {
    "Exporters": {
      "OTLP": {
        "Enabled": true,
        "Endpoint": "http://otel-collector:4317"
      }
    }
  }
}
```

### Jaeger Setup (Optional)

#### Docker Compose

```yaml
version: '3'
services:
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # Jaeger UI
      - "4317:4317"    # OTLP gRPC receiver
      - "4318:4318"    # OTLP HTTP receiver
    environment:
      - COLLECTOR_OTLP_ENABLED=true
```

#### Configuration

```json
{
  "OpenTelemetry": {
    "Exporters": {
      "OTLP": {
        "Enabled": true,
        "Endpoint": "http://localhost:4317"
      }
    }
  }
}
```

Access Jaeger UI at: `http://localhost:16686`

### Prometheus Setup (Optional)

For metrics visualization:

#### Docker Compose

```yaml
version: '3'
services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
```

#### prometheus.yml

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'otel-collector'
    static_configs:
      - targets: ['otel-collector:8889']
```

## Sampling Configuration

Sampling controls what percentage of requests are traced:

- **Development**: `1.0` (100%) - Trace everything
- **Staging**: `0.5` (50%) - Trace half
- **Production**: `0.1` (10%) - Trace 10% to reduce overhead

```json
{
  "OpenTelemetry": {
    "Sampling": {
      "Probability": 0.1
    }
  }
}
```

## Best Practices

### 1. Naming Conventions

- **Activity Names**: Use `{Domain}.{Operation}` format (e.g., `Product.Create`, `Wallet.Transfer`)
- **Tag Names**: Use lowercase with dots (e.g., `product.id`, `wallet.amount`)
- **Metric Names**: Use PascalCase for clarity (e.g., `ProductsCreated`, `WalletOperationDuration`)

### 2. Tag Guidelines

- Add relevant business context as tags
- Keep tag cardinality low (avoid high-cardinality values like timestamps)
- Use consistent tag names across services
- Include tenant_id for multi-tenant operations

### 3. Error Handling

Always set activity status and record exception details:

```csharp
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.SetTag("exception.type", ex.GetType().FullName);
    activity?.SetTag("exception.message", ex.Message);
    activity?.SetTag("exception.stacktrace", ex.StackTrace);
    throw;
}
```

### 4. Performance Considerations

- Use `using var activity = ...` for automatic disposal
- Check if activity is null before setting tags (`activity?.SetTag(...)`)
- Use sampling in production to reduce overhead
- Avoid creating spans for very frequent operations (>1000/sec)

## Troubleshooting

### No Traces Appearing

1. **Check sampling rate**: Ensure it's not set to 0
2. **Verify exporter configuration**: Console exporter should be enabled in development
3. **Check service name**: Must match configuration
4. **Verify OTel is installed**: Check `InstallOpenTelemetry()` call in Program.cs

### Missing TraceId in Logs

1. **Verify Serilog.Enrichers.Span**: Package must be installed
2. **Check enrichers**: Ensure `"WithSpan"` is in Serilog configuration
3. **Verify log template**: Must include `{TraceId}` and `{SpanId}` placeholders

### High Memory Usage

1. **Reduce sampling rate**: Lower the probability in production
2. **Check batch size**: OTLP exporter batching configuration
3. **Review custom spans**: Ensure proper disposal with `using`

## Example: Full Service Implementation

```csharp
public class ProductService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ProductService> _logger;

    public async Task<Result<Product>> CreateAsync(CreateProductRequest request)
    {
        using var activity = ActivitySources.Product.StartActivity("Product.Create");
        activity?.SetTag("product.name", request.Name);
        activity?.SetTag("product.price", request.Price);
        activity?.SetTag("product.category_id", request.CategoryId);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var productId = Guid.NewGuid();
            activity?.SetTag("product.id", productId);
            
            _logger.LogInformation("Creating product {ProductId}", productId);
            
            var product = new Product
            {
                Id = productId,
                Name = request.Name,
                Price = request.Price,
                CategoryId = request.CategoryId
            };
            
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();
            
            stopwatch.Stop();
            
            // Record metrics
            XFrameworkMetrics.ProductsCreated.Add(1,
                new KeyValuePair<string, object?>("category_id", request.CategoryId.ToString()));
            XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("result", "success"));
            
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("operation.duration_ms", stopwatch.ElapsedMilliseconds);
            
            _logger.LogInformation("Product {ProductId} created successfully in {Duration}ms", 
                productId, stopwatch.ElapsedMilliseconds);
            
            return Result<Product>.Success(product, 201, "Product created successfully");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("result", "error"));
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            activity?.SetTag("exception.stacktrace", ex.StackTrace);
            
            _logger.LogError(ex, "Failed to create product");
            
            return Result<Product>.Failure("An error occurred while creating the product", 500);
        }
    }
}
```

## Resources

- [OpenTelemetry Official Documentation](https://opentelemetry.io/docs/)
- [OpenTelemetry .NET SDK](https://github.com/open-telemetry/opentelemetry-dotnet)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Prometheus Documentation](https://prometheus.io/docs/)

## Related Documentation

- [Structured Logging Standards](../conventions/logging-standards.md)
- [Correlation ID Middleware](../../../src/Kernel/XFramework.Core/Middlewares/CorrelationIdMiddleware.cs)
- [Activity Sources Implementation](../../../src/Kernel/XFramework.Core/Observability/ActivitySources.cs)
- [Metrics Implementation](../../../src/Kernel/XFramework.Core/Observability/XFrameworkMetrics.cs)

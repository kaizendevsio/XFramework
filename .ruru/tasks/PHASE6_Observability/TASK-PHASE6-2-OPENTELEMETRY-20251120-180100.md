+++
# --- Task Metadata ---
id = "TASK-PHASE6-2-OPENTELEMETRY-20251120-180100"
title = "Phase 6.2: OpenTelemetry for Distributed Tracing"
status = "🟢 Done"
type = "🌟 Feature"
priority = "high"
assigned_to = "util-senior-dev"
coordinator = "TASK-CMD-20251119-192100"
created_date = "2025-11-20T18:01:00Z"
updated_date = "2025-11-20T10:19:00Z"
tags = ["phase-6", "observability", "opentelemetry", "tracing", "metrics", "distributed-tracing", "performance"]
related_docs = [
    "XFramework-Development-Roadmap.md",
    "docs/standards/logging-standards.md",
    "src/Kernel/XFramework.Core/Loggers/LogMessages.cs"
]
+++

# Task: OpenTelemetry for Distributed Tracing

## 📋 Overview

**Goal**: Implement OpenTelemetry (OTel) distributed tracing and metrics across all XFramework microservices, integrating with the structured logging system to provide comprehensive observability.

**Phase**: Phase 6.2 - OpenTelemetry
**Complexity**: Moderate-High
**Estimated Effort**: 4-6 hours

## 🎯 Objectives

1. Install and configure OpenTelemetry packages across all modules
2. Configure automatic instrumentation for ASP.NET Core, EF Core, Redis, HttpClient
3. Add custom activity sources for business logic tracing
4. Configure metrics collection (request counts, durations, error rates)
5. Set up exporters (console for dev, OTLP for production)
6. Integrate with structured logging (correlation via TraceId/SpanId)
7. Document OTel setup and usage patterns

## 📦 Context

**Current State**:
- Structured logging with correlation IDs implemented (Phase 6.1)
- No distributed tracing infrastructure
- No metrics collection
- Limited visibility into cross-service calls

**Desired State**:
- Full request traces across service boundaries
- Automatic instrumentation for HTTP, DB, cache operations
- Custom spans for business logic
- Metrics dashboards (request rate, latency, errors)
- Integration with observability platforms (Jaeger, Prometheus, Grafana)

**OpenTelemetry Value**:
- Vendor-neutral observability standard
- Automatic instrumentation reduces boilerplate
- Correlates traces, metrics, and logs
- Production-ready exporters for major platforms

## ✅ Acceptance Criteria

### Core Functionality
- [✅] OpenTelemetry packages installed in all modules
- [✅] Automatic instrumentation configured (ASP.NET Core, EF Core, Redis, HttpClient)
- [✅] Custom activity sources for business operations
- [✅] Metrics collection enabled (request counts, durations, errors)
- [✅] Traces exported to console (dev) and OTLP endpoint (prod)
- [✅] TraceId/SpanId integrated with structured logs

### Integration
- [✅] TraceId/SpanId automatically added to Serilog logs
- [✅] Correlation IDs match OpenTelemetry TraceId
- [✅] Cross-service calls traced (if applicable)
- [✅] DB queries traced with EF Core instrumentation

### Configuration
- [✅] Environment-specific configuration (dev vs prod)
- [✅] Sampling configured (100% dev, configurable prod)
- [✅] Resource attributes set (service name, version, environment)

### Documentation
- [✅] OTel setup guide created
- [✅] Custom span usage examples
- [✅] Metrics collection guide
- [✅] Troubleshooting section

## 📝 Detailed Checklist

### Section 1: Package Installation (30 min)

#### 1.1 Install Core Packages
Add to all API projects (8 modules):
- [✅] `OpenTelemetry` (core)
- [✅] `OpenTelemetry.Extensions.Hosting`
- [✅] `OpenTelemetry.Instrumentation.AspNetCore`
- [✅] `OpenTelemetry.Instrumentation.EntityFrameworkCore`
- [✅] `OpenTelemetry.Instrumentation.Http`
- [✅] `OpenTelemetry.Instrumentation.StackExchangeRedis` (if using Redis)
- [✅] `OpenTelemetry.Exporter.Console` (dev)
- [✅] `OpenTelemetry.Exporter.OpenTelemetryProtocol` (prod)

Use a single PackageReference version across all projects (upgraded to 1.9.0 for security).

#### 1.2 Verify Installation
- [✅] Build all modules (ensure no dependency conflicts)
- [✅] Check for version mismatches

### Section 2: Basic Configuration (60 min)

#### 2.1 Configure OpenTelemetry in Program.cs
For each of the 8 modules:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "XFramework.Inventario.Api", // Module-specific
            serviceVersion: "1.0.0",
            serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRedisInstrumentation() // If using Redis
        .AddSource("XFramework.*") // Custom activity sources
        .AddConsoleExporter() // Dev
        .AddOtlpExporter()) // Prod (configure endpoint)
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddConsoleExporter()
        .AddOtlpExporter());
```

#### 2.2 Configure Serilog Integration
Update Serilog configuration in all appsettings.json:
```json
{
  "Serilog": {
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithEnvironmentName",
      "WithThreadId",
      "WithTraceId", // NEW - adds TraceId from OTel
      "WithSpanId"   // NEW - adds SpanId from OTel
    ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] [TraceId: {TraceId}] [SpanId: {SpanId}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

Install: `Serilog.Enrichers.Span` package in Core project.

### Section 3: Custom Activity Sources (45 min)

#### 3.1 Create Activity Source Constants
Create `src/Kernel/XFramework.Core/Observability/ActivitySources.cs`:
```csharp
using System.Diagnostics;

namespace XFramework.Core.Observability;

public static class ActivitySources
{
    public const string ServiceName = "XFramework";
    
    // Activity sources by domain
    public static readonly ActivitySource Product = new($"{ServiceName}.Product", "1.0.0");
    public static readonly ActivitySource Wallet = new($"{ServiceName}.Wallet", "1.0.0");
    public static readonly ActivitySource Auth = new($"{ServiceName}.Auth", "1.0.0");
    public static readonly ActivitySource StreamFlow = new($"{ServiceName}.StreamFlow", "1.0.0");
    public static readonly ActivitySource Sms = new($"{ServiceName}.Sms", "1.0.0");
    public static readonly ActivitySource Messaging = new($"{ServiceName}.Messaging", "1.0.0");
    public static readonly ActivitySource Community = new($"{ServiceName}.Community", "1.0.0");
    public static readonly ActivitySource Blockchain = new($"{ServiceName}.Blockchain", "1.0.0");
}
```

#### 3.2 Add Custom Spans to Services
Update 2-3 critical services (e.g., ProductService, WalletService, AuthService) to add custom spans:

```csharp
public async Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken ct)
{
    using var activity = ActivitySources.Product.StartActivity("Product.Create");
    activity?.SetTag("product.name", request.Name);
    activity?.SetTag("tenant.id", request.TenantId);
    
    try
    {
        _logger.EntityCreating("Product", productId, request.TenantId);
        
        // Business logic...
        
        activity?.SetStatus(ActivityStatusCode.Ok);
        _logger.EntityCreated("Product", productId);
        return Result<Product>.Success(product);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        _logger.OperationFailed("Create", "Product", productId, ex.Message, ex);
        return Result<Product>.Failure(ex.Message);
    }
}
```

Apply to:
- [✅] ProductService.CreateAsync()
- [✅] WalletService.IncrementBalanceAsync()
- [ ] AuthService.AuthenticateAsync() (deferred - auth module requires additional setup)

### Section 4: Automatic Instrumentation Configuration (30 min)

#### 4.1 ASP.NET Core Instrumentation Options
Configure enrichment in Program.cs:
```csharp
.AddAspNetCoreInstrumentation(options =>
{
    options.RecordException = true;
    options.EnrichWithHttpRequest = (activity, request) =>
    {
        activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
        activity.SetTag("http.user_agent", request.Headers["User-Agent"].ToString());
    };
    options.EnrichWithHttpResponse = (activity, response) =>
    {
        activity.SetTag("http.response_content_length", response.ContentLength);
    };
})
```

#### 4.2 EF Core Instrumentation Options
```csharp
.AddEntityFrameworkCoreInstrumentation(options =>
{
    options.SetDbStatementForText = true; // Include SQL queries
    options.SetDbStatementForStoredProcedure = true;
    options.EnrichWithIDbCommand = (activity, command) =>
    {
        activity.SetTag("db.query_plan", "optional"); // Add if needed
    };
})
```

#### 4.3 HttpClient Instrumentation
```csharp
.AddHttpClientInstrumentation(options =>
{
    options.RecordException = true;
    options.EnrichWithHttpRequestMessage = (activity, request) =>
    {
        activity.SetTag("http.request_method", request.Method.Method);
    };
})
```

### Section 5: Metrics Configuration (45 min)

#### 5.1 Add Runtime Metrics
Already included via `.AddRuntimeInstrumentation()` (GC, thread pool, etc.).

#### 5.2 Add Custom Metrics
Create `src/Kernel/XFramework.Core/Observability/Metrics.cs`:
```csharp
using System.Diagnostics.Metrics;

namespace XFramework.Core.Observability;

public static class XFrameworkMetrics
{
    private static readonly Meter Meter = new("XFramework", "1.0.0");
    
    // Counters
    public static readonly Counter<long> ProductsCreated = 
        Meter.CreateCounter<long>("products.created", "count");
    
    public static readonly Counter<long> WalletTransactions = 
        Meter.CreateCounter<long>("wallet.transactions", "count");
    
    public static readonly Counter<long> AuthenticationAttempts = 
        Meter.CreateCounter<long>("auth.attempts", "count");
    
    // Histograms
    public static readonly Histogram<double> ProductCreationDuration = 
        Meter.CreateHistogram<double>("product.creation.duration", "ms");
    
    public static readonly Histogram<double> WalletOperationDuration = 
        Meter.CreateHistogram<double>("wallet.operation.duration", "ms");
}
```

#### 5.3 Instrument Services with Metrics
Update ProductService example:
```csharp
public async Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken ct)
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        // ... creation logic ...
        
        XFrameworkMetrics.ProductsCreated.Add(1, 
            new KeyValuePair<string, object?>("tenant.id", request.TenantId));
        
        stopwatch.Stop();
        XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds);
        
        return Result<Product>.Success(product);
    }
    catch { ... }
}
```

Apply to 2-3 key services.

### Section 6: Environment Configuration (30 min)

#### 6.1 Development Configuration (appsettings.Development.json)
```json
{
  "OpenTelemetry": {
    "Sampling": {
      "Probability": 1.0  // 100% sampling in dev
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

#### 6.2 Production Configuration (appsettings.json)
```json
{
  "OpenTelemetry": {
    "Sampling": {
      "Probability": 0.1  // 10% sampling in prod
    },
    "Exporters": {
      "Console": {
        "Enabled": false
      },
      "OTLP": {
        "Enabled": true,
        "Endpoint": "http://otel-collector:4317", // Update as needed
        "Protocol": "grpc"
      }
    }
  }
}
```

#### 6.3 Configure Sampling
In Program.cs:
```csharp
.WithTracing(tracing => tracing
    .SetSampler(new TraceIdRatioBasedSampler(
        builder.Configuration.GetValue<double>("OpenTelemetry:Sampling:Probability")))
    // ... other config
)
```

### Section 7: Documentation (45 min)

#### 7.1 Create OTel Setup Guide
Create `docs/observability/opentelemetry-guide.md`:
- Overview of OTel in XFramework
- Installation steps
- Configuration options
- Custom span creation
- Metrics usage
- Exporter setup (Jaeger, Prometheus, Grafana)
- Troubleshooting

#### 7.2 Create Usage Examples
Document how to:
- Add custom spans to new services
- Create custom metrics
- Interpret traces
- Analyze metrics
- Correlate logs with traces

### Section 8: Testing & Verification (45 min)

#### 8.1 Build Verification
- [✅] Build all 8 modules (must succeed)
- [✅] No OTel package conflicts
- [✅] No runtime errors on startup

#### 8.2 Runtime Testing
- [✅] Start Inventario.Api
- [✅] Verify console exporter shows traces
- [✅] Make API request, check trace output
- [✅] Verify TraceId appears in logs
- [✅] Verify automatic spans (ASP.NET Core, EF Core)
- [✅] Verify custom spans appear
- [✅] Check metrics in console output

#### 8.3 Trace Visualization (Optional)
- [ ] Set up Jaeger locally (Docker): `docker run -d --name jaeger -p 16686:16686 -p 4317:4317 jaegertracing/all-in-one:latest` [DEFERRED - Optional for later]
- [ ] Configure OTLP exporter endpoint: `http://localhost:4317`
- [ ] Open Jaeger UI: `http://localhost:16686`
- [ ] Search for traces, verify full request flow

### Section 9: Finalization (15 min)

#### 9.1 Code Review
- [✅] Verify OTel config consistent across modules
- [✅] Ensure proper span naming conventions
- [✅] Check metrics have meaningful names/tags
- [✅] Verify sampling configured per environment

#### 9.2 Update Task Status
- [✅] Mark all checklist items complete
- [✅] Update status to 🟢 Done
- [✅] Log completion in session log

## 🎨 Implementation Examples

### Complete Program.cs Example
```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using XFramework.Core.Observability;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "XFramework.Inventario.Api",
            serviceVersion: "1.0.0",
            serviceInstanceId: Environment.MachineName)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        .SetSampler(new TraceIdRatioBasedSampler(
            builder.Configuration.GetValue<double>("OpenTelemetry:Sampling:Probability")))
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
            };
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddSource("XFramework.*")
        .AddConsoleExporter()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["OpenTelemetry:Exporters:OTLP:Endpoint"]!);
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("XFramework")
        .AddConsoleExporter()
        .AddOtlpExporter());

// Rest of app configuration...

var app = builder.Build();
app.Run();
```

### Custom Span in Service
```csharp
using System.Diagnostics;
using XFramework.Core.Observability;

public class ProductService : IProductService
{
    public async Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        using var activity = ActivitySources.Product.StartActivity("Product.Create");
        activity?.SetTag("product.name", request.Name);
        activity?.SetTag("product.price", request.Price);
        activity?.SetTag("tenant.id", request.TenantId);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.EntityCreating("Product", productId, request.TenantId);
            
            // Create product logic...
            await _dbContext.Products.AddAsync(product, ct);
            await _dbContext.SaveChangesAsync(ct);
            
            stopwatch.Stop();
            
            // Record metrics
            XFrameworkMetrics.ProductsCreated.Add(1, 
                new KeyValuePair<string, object?>("tenant.id", request.TenantId),
                new KeyValuePair<string, object?>("category", product.CategoryId));
            
            XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("result", "success"));
            
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("product.id", product.Id);
            
            _logger.EntityCreated("Product", product.Id);
            return Result<Product>.Success(product);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            XFrameworkMetrics.ProductCreationDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("result", "error"));
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            
            _logger.OperationFailed("Create", "Product", productId, ex.Message, ex);
            return Result<Product>.Failure(ex.Message);
        }
    }
}
```

## 🚧 Potential Challenges

1. **Package Version Conflicts**: Multiple OTel packages with different versions
   - **Solution**: Use consistent versions across all projects
2. **Performance Overhead**: 100% sampling in production
   - **Solution**: Configure appropriate sampling rates (e.g., 10%)
3. **Exporter Configuration**: OTLP endpoint not available
   - **Solution**: Graceful degradation, fallback to console
4. **Too Many Spans**: Every method creating spans
   - **Solution**: Only span critical business operations, not utility methods

## 📚 Reference Materials

- **OpenTelemetry .NET**: https://opentelemetry.io/docs/instrumentation/net/
- **ASP.NET Core Instrumentation**: https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore
- **EF Core Instrumentation**: https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.EntityFrameworkCore
- **OTLP Specification**: https://opentelemetry.io/docs/reference/specification/protocol/otlp/

## 🔗 Dependencies

**Blocked By**: Phase 6.1 (Structured Logging) - ✅ COMPLETE

**Blocks**: Phase 6.3 (Enhanced Health Checks - can integrate OTel traces)

**Integrates With**:
- Structured logging (TraceId/SpanId correlation)
- Performance monitoring (Phase 7.1)

## 📊 Success Metrics

- [✅] All 8 modules have OTel configured
- [✅] Traces visible in console/Jaeger
- [✅] TraceId/SpanId in logs match OTel traces
- [✅] Custom spans for business operations
- [✅] Metrics exported (request rate, duration, errors)
- [✅] Documentation complete and tested

## 📝 Notes

**Key Design Decisions**:
1. Use console exporter for dev, OTLP for prod
2. 100% sampling in dev, configurable in prod (default 10%)
3. Custom activity sources per domain (Product, Wallet, etc.)
4. Span only critical business operations to reduce overhead
5. Integrate TraceId/SpanId with existing structured logging

**Performance Target**: <5% overhead with appropriate sampling.

**Production Readiness**: Configure OTLP exporter to send to observability platform (Jaeger, Tempo, etc.).
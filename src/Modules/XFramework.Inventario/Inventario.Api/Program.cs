using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

// Configure OpenTelemetry for distributed tracing and metrics
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Inventario.Api");

// Configure comprehensive health checks
builder.Services.AddXFrameworkHealthChecks<DbContext>(
    builder.Configuration,
    "Inventario");

// Register caching services (required by ProductService)
builder.Services.AddMemoryCaching();

// Register optional dependencies as null for HybridCacheService when using memory-only caching
builder.Services.AddSingleton<IDistributedCache>(sp => null!);
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => null!);

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Register DataContext handler for entity query/mutation via Bolt
builder.Services.AddDataContextHandler(typeof(Program).Assembly);

// Auto-discover and register all generated services
builder.Services.AddGeneratedServices();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.EnsureDatabase<DbContext>();

// Auto-discover and map entity-generated endpoints (from [GenerateEndpoints] attribute)
EndpointDiscoveryExtensions.MapGeneratedEndpoints(app);

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
Inventario.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(app);

// Map health check endpoints (liveness, readiness, and detailed health)
app.MapXFrameworkHealthChecks("Inventario");

app.Run();

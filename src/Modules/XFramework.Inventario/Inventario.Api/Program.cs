using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

// Configure OpenTelemetry for distributed tracing and metrics
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Inventario.Api");

// Register caching services (required by ProductService)
builder.Services.AddMemoryCaching();

// Register optional dependencies as null for HybridCacheService when using memory-only caching
builder.Services.AddSingleton<IDistributedCache>(sp => null!);
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => null!);

// Auto-discover and register all generated services
builder.Services.AddGeneratedServices();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.EnsureDatabase<DbContext>();

// Auto-discover and map all generated endpoints
app.MapGeneratedEndpoints();

app.Run();
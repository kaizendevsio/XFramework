using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using Inventario.Api.Features.Products.Update;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);

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

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();
app.UseTenantModuleFeatureGate(options =>
{
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/warehouses", "warehousing");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/locations", "warehousing");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/stock/balances", "stock_balances");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/stock/movements", "movements");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/stock/post", "movements");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/reservations", "reservations");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/allocations", "reservations");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/lots", "traceability");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/reorder-rules", "planning");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/planning", "planning");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api/inventario/reports", "reporting");
    options.RequireFeature(TenantModuleFeatureKeys.Inventario, "/api");
});

app.EnsureDatabase<DbContext>();

// Map health check endpoints (liveness, readiness, and detailed health)
app.MapXFrameworkHealthChecks("Inventario");
app.MapApiDocumentation();

// Map real product feature endpoints behind authorization.
var authorizedRoutes = app.MapGroup("").RequireAuthorization();
Inventario.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(authorizedRoutes);
UpdateProductEndpoint.Map(authorizedRoutes);

app.Run();

using FluentValidation;
using SmsGateway.Api.Generated;
using SmsGateway.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);

// Configure OpenTelemetry for distributed tracing and metrics
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.SmsGateway.Api");

// Configure comprehensive health checks
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "SmsGateway");

// Register services - CachingService MUST be Singleton (uses in-memory ConcurrentDictionary)
builder.Services.AddSingleton<ICachingService, CachingService>();
builder.Services.AddScoped<ISmsService, SmsService>();

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

// Map health check endpoints (liveness, readiness, and detailed health)
app.MapXFrameworkHealthChecks("SmsGateway");
app.MapApiDocumentation();

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
app.MapGeneratedEndpoints();

app.Run();

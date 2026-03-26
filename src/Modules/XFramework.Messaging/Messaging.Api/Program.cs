using FluentValidation;
using Messaging.Api.Generated;
using Messaging.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;

var builder = XApplication.Configure<Program>();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Messaging.Api");

// Add health checks
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Messaging");

// Register services
builder.Services.AddScoped<IMessagingService, MessagingService>();

// Register validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Rate limiting — global 100/min per IP
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline
app.UseCorrelationId();
app.UseXFrameworkRateLimiting();

// Database migration
app.EnsureDatabase<AppDbContext>();

// Map health check endpoints
app.MapXFrameworkHealthChecks("Messaging");
app.MapApiDocumentation();

// Map feature endpoints (source-generated from [MapPost/Patch/...] attributes)
app.MapGeneratedEndpoints();

app.Run();

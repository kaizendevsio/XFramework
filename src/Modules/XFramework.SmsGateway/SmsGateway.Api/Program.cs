using FluentValidation;
using SmsGateway.Api.Features.Sms;
using SmsGateway.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

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

app.UseCustomRequestsInAssembly<SmsGatewayBaseRequest>();

// Map SMS endpoints
app.MapSmsEndpoints();

// Map health check endpoints (liveness, readiness, and detailed health)
app.MapXFrameworkHealthChecks("SmsGateway");
app.MapApiDocumentation();

app.Run();
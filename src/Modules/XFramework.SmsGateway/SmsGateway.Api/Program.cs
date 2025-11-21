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

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.UseCustomRequestsInAssembly<SmsGatewayBaseRequest>();

// Map health check endpoints (liveness, readiness, and detailed health)
app.MapXFrameworkHealthChecks("SmsGateway");

app.Run();
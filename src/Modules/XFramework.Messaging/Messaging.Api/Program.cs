using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Messaging.Api");

// Add health checks
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Messaging");

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

// Database migration and custom request handling
app.EnsureDatabase<AppDbContext>();
app.UseCustomRequestsInAssembly<MessagingBaseRequest>();

// Map health check endpoints
app.MapXFrameworkHealthChecks("Messaging");

app.Run();
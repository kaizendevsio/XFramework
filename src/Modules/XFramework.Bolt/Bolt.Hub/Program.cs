using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Domain.Contexts;

var builder = XApplication.Configure<Program>();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Bolt.Hub");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Bolt");

// Rate limiting — global 100/min per IP
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();
app.UseXFrameworkRateLimiting();

app.UseAppServices();
app.MapXFrameworkHealthChecks("Bolt");

app.MapApiDocumentation();

app.Run();
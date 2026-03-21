using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;

var builder = XApplication.Configure<Program>();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.StreamFlow.Stream");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "StreamFlow");

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.UseAppServices();
app.MapXFrameworkHealthChecks("StreamFlow");

app.MapApiDocumentation();

app.Run();
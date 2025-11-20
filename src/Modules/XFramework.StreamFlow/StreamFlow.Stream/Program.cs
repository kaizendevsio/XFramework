using XFramework.Core.Extensions;

var builder = WebApplication.CreateBuilder();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.StreamFlow.Stream");

var app = XApplication
    .Build<Program>()
    .UseAppServices();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.Run();
using XFramework.Extensions;
using XFramework.Core.Extensions;

var builder = WebApplication.CreateBuilder();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Community.Api");

var app = XApplication
    .Build<Program>()
    .GenerateMinimalApi();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.Run();
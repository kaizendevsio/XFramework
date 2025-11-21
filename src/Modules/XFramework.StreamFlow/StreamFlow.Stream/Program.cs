using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.StreamFlow.Stream");

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.UseAppServices();

app.Run();
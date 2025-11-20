using XFramework.Core.Extensions;

var builder = WebApplication.CreateBuilder();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.SmsGateway.Api");

var app = XApplication
    .Build<Program>()
    .UseCustomRequestsInAssembly<SmsGatewayBaseRequest>();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.Run();
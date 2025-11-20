using XFramework.Core.Extensions;

var builder = WebApplication.CreateBuilder();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Wallets.Api");

var app = XApplication
    .Build<Program>()
    .GenerateMinimalApi()
    .UseCustomRequestsInAssembly<WalletsBaseRequest>();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

app.Run();
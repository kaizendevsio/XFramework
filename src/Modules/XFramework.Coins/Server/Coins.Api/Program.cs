using Coins.Api.Features.Blockchain;
using XFramework.Extensions;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

// Add OpenTelemetry if available
try
{
    builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Coins.Api");
}
catch
{
    // OpenTelemetry not configured, continue
}

var app = (WebApplication)builder.Build();

// Add correlation ID middleware
app.UseCorrelationId();

// Map VSA Feature Endpoints
app.MapBlockchainEndpoints();

app.Run();
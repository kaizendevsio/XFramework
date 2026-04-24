using Coins.Api.Generated;
using XFramework.Extensions;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);

// Add OpenTelemetry if available
try
{
    builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Coins.Api");
}
catch
{
    // OpenTelemetry not configured, continue
}

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware
app.UseCorrelationId();

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
app.MapGeneratedEndpoints();

app.Run();

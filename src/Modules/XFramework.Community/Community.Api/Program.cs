using Community.Api.Generated;
using XFramework.Extensions;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;

var builder = XApplication.Configure<Program>();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Community.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Community");

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Rate limiting — global 100/min per IP
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.EnsureDatabase<AppDbContext>();
app.MapXFrameworkHealthChecks("Community");

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
app.MapGeneratedEndpoints();

app.Run();

using FluentValidation;
using Messaging.Api.Features.Messages;
using Messaging.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Messaging.Api");

// Add health checks
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Messaging");

// Register services
builder.Services.AddScoped<IMessagingService, MessagingService>();

// Register validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline for request tracing
app.UseCorrelationId();

// Database migration and custom request handling
app.EnsureDatabase<AppDbContext>();
app.UseCustomRequestsInAssembly<MessagingBaseRequest>();

// Map VSA feature endpoints
app.MapMessageEndpoints();

// Map health check endpoints
app.MapXFrameworkHealthChecks("Messaging");
app.MapApiDocumentation();

app.Run();
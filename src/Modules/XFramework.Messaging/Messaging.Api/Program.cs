using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using Messaging.Api.Generated;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Messaging.Api");

// Add health checks
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Messaging");

// Register services
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IThreadService, ThreadService>();
builder.Services.AddScoped<IMessagingRequestContextResolver, MessagingRequestContextResolver>();
builder.Services.AddScoped<IMessagingRealtimePublisher, MessagingRealtimePublisher>();
builder.Services.AddScoped<IMessagingNotificationFanout, MessagingNotificationFanout>();
builder.Services.AddScoped<IMessagingSettingsService, MessagingSettingsService>();
builder.Services.AddScoped<IMessagingTemplateService, MessagingTemplateService>();

// Register validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Rate limiting — global 100/min per IP
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline
app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(options =>
{
    options.RequireFeature(TenantModuleFeatureKeys.MessagingChat, "/api/threads");
    options.RequireFeature(TenantModuleFeatureKeys.MessagingChat, "/api/messages");
    options.RequireFeature(TenantModuleFeatureKeys.MessagingChat, "/api/messaging");
});

// Database migration
app.EnsureDatabase<AppDbContext>();

// Map health check endpoints
app.MapXFrameworkHealthChecks("Messaging");
app.MapApiDocumentation();

// Map feature endpoints (source-generated from [MapPost/Patch/...] attributes)
var securedMessagingEndpoints = app.MapGroup(string.Empty).RequireAuthorization();
securedMessagingEndpoints.MapGeneratedEndpoints();

app.Run();

using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using Communications.Api.Generated;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Communications.Api");

// Add health checks
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Communications");

// Register services
builder.Services.AddScoped<ICommunicationsService, CommunicationsService>();
builder.Services.AddScoped<IThreadService, ThreadService>();
builder.Services.AddScoped<ICommunicationsRequestContextResolver, CommunicationsRequestContextResolver>();
builder.Services.AddScoped<ICommunicationsRealtimePublisher, CommunicationsRealtimePublisher>();
builder.Services.AddScoped<ICommunicationsTransientRealtimePublisher, CommunicationsTransientRealtimePublisher>();
builder.Services.AddScoped<ICommunicationsNotificationFanout, CommunicationsNotificationFanout>();
builder.Services.AddScoped<ICommunicationsSettingsService, CommunicationsSettingsService>();
builder.Services.AddScoped<ICommunicationsPolicyService, CommunicationsPolicyService>();
builder.Services.AddSingleton<ICommunicationsActionRateLimiter, CommunicationsActionRateLimiter>();
builder.Services.AddScoped<ICommunicationsModerationService, CommunicationsModerationService>();
builder.Services.AddScoped<ICommunicationsTemplateService, CommunicationsTemplateService>();
builder.Services.AddScoped<ICommunicationsAdminReadService, CommunicationsAdminReadService>();

// Register validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Rate limiting - global 100/min per IP
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

// Add correlation ID middleware early in the pipeline
app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(options =>
{
    options.RequireFeature(TenantModuleFeatureKeys.Communications, "/api/communications/admin");
    options.RequireFeature(TenantModuleFeatureKeys.Communications, "/api/communications/settings");
    options.RequireFeature(TenantModuleFeatureKeys.Communications, "/api/communications/templates");
    options.RequireFeature(TenantModuleFeatureKeys.CommunicationsChat, "/api/communications/threads");
    options.RequireFeature(TenantModuleFeatureKeys.CommunicationsChat, "/api/communications/messages");
    options.RequireFeature(TenantModuleFeatureKeys.CommunicationsChat, "/api/communications/realtime");
    options.RequireFeature(TenantModuleFeatureKeys.CommunicationsChat, "/api/communications/blocks");
});

// Database migration
app.EnsureDatabase<AppDbContext>();

// Map health check endpoints
app.MapXFrameworkHealthChecks("Communications");
app.MapApiDocumentation();

// Map feature endpoints (source-generated from [MapPost/Patch/...] attributes)
var securedCommunicationsEndpoints = app.MapGroup(string.Empty).RequireAuthorization();
securedCommunicationsEndpoints.MapGeneratedEndpoints();

app.Run();

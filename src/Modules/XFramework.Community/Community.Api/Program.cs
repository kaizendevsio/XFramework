using Community.Api.Generated;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Extensions;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Community.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Community");

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Register DataContext handler for entity query/mutation via Bolt
builder.Services.AddDataContextHandler(typeof(Program).Assembly);

// Rate limiting: global 100/min per IP
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(options =>
{
    options.RequireFeature(TenantModuleFeatureKeys.Notifications, "/api/community/notifications");
    options.RequireFeature(TenantModuleFeatureKeys.Notifications, "/api/community-notifications");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-identities");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-identity-types");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-identity-files");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-identity-file-types");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-content");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-content-types");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-content-files");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-content-reactions");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-content-reaction-types");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-connections");
    options.RequireFeature(TenantModuleFeatureKeys.Community, "/api/community-connection-types");
});
app.EnsureDatabase<AppDbContext>();
app.MapXFrameworkHealthChecks("Community");

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
// through an authorized route group so manual Community routes are not anonymous.
var authorizedCommunityRoutes = app.MapGroup("").RequireAuthorization();
authorizedCommunityRoutes.MapGeneratedEndpoints();

app.Run();

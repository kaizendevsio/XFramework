using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using Notifications.Api.Generated;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Notifications.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Notifications");

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddDataContextHandler(typeof(Program).Assembly);
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(options =>
    options.RequireFeature(TenantModuleFeatureKeys.Notifications, "/api/notifications"));
app.EnsureDatabase<AppDbContext>();

app.MapXFrameworkHealthChecks("Notifications");
var securedNotificationEndpoints = app.MapGroup(string.Empty).RequireAuthorization();
securedNotificationEndpoints.MapGeneratedEndpoints();
app.MapApiDocumentation();

app.Run();

public partial class Program;

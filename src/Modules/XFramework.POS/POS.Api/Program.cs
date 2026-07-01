using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using Bolt.Client;
using POS.Api.Generated;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Domain.Contexts;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.POS.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "POS");

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddDataContextHandler(typeof(Program).Assembly);
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

RegisterGeneratedBoltHandlers(app);

app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(options =>
{
    options.RequireFeature(TenantModuleFeatureKeys.PosCarts, "/api/pos/carts");
    options.RequireFeature(TenantModuleFeatureKeys.Pos, "/api/pos");
});

app.MapXFrameworkHealthChecks("POS");
var securedPosEndpoints = app.MapGroup(string.Empty).RequireAuthorization();
securedPosEndpoints.MapGeneratedEndpoints();
app.MapApiDocumentation();

app.Run();

static void RegisterGeneratedBoltHandlers(WebApplication app)
{
    var client = app.Services.GetRequiredService<BoltClient>();
    var logger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("POS.GeneratedBoltHandlers");
    var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

    BoltHandlerRegistry.RegisterAll(client, logger, scopeFactory);
}

public partial class Program;

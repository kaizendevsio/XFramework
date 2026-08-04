using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Extensions;
using Attendance.Api.Generated;
using Bolt.Client;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);
builder.Services.AddIdentityServerSessionValidation();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Attendance.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Attendance");

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddDataContextHandler(typeof(Program).Assembly);
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

RegisterGeneratedBoltHandlers(app);

app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(options =>
    options.RequireFeature(TenantModuleFeatureKeys.Attendance, "/api/attendance"));

app.MapXFrameworkHealthChecks("Attendance");
var securedAttendanceEndpoints = app.MapGroup(string.Empty).RequireAuthorization();
securedAttendanceEndpoints.MapGeneratedEndpoints();
app.MapApiDocumentation();

app.Run();

static void RegisterGeneratedBoltHandlers(WebApplication app)
{
    var client = app.Services.GetRequiredService<BoltClient>();
    var logger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Attendance.GeneratedBoltHandlers");
    var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

    BoltHandlerRegistry.RegisterAll(client, logger, scopeFactory);
}

public partial class Program;

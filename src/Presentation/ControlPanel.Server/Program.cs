using System.Text.Json;
using BlazorBlueprint.Components;
using ControlPanel.Server.Health;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging — ZLogger console (lifecycle only) + Seq (everything including Bolt RPC payloads)
builder.Logging.AddXFrameworkLogging(builder.Configuration);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// BlueprintUI
builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
{
    options.DefaultBaseColor = BaseColor.Slate;
    options.DefaultPrimaryColor = PrimaryColor.Blue;
    options.DefaultDarkMode = true;
    options.DetectSystemPreference = true;
    options.DefaultRadius = 0.5;
    options.PersistToLocalStorage = true;
});

// Bolt — thin binary RPC transport to microservices
builder.Services.AddXFrameworkBoltClient(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddCheck(
        "controlpanel-live",
        () => HealthCheckResult.Healthy("ControlPanel is running."),
        tags: ["live"])
    .AddCheck<BoltClientHealthCheck>(
        "controlpanel-bolt",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

// Service wrappers — auto-generated CRUD + custom operations for each microservice
builder.Services.AddIdentityServerWrapperServices();
builder.Services.AddWalletsWrapperServices();

// IDataContext — universal query layer routed through service wrappers
builder.Services.AddRemoteDataContext();

// Tenant filter state (sidebar selection)
builder.Services.AddScoped<ControlPanel.Server.Services.TenantFilterService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapStaticAssets();
app.MapRazorComponents<ControlPanel.Server.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var response = new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds,
        timestamp = DateTime.UtcNow,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.TotalMilliseconds,
            tags = entry.Value.Tags,
            data = entry.Value.Data,
            exception = entry.Value.Exception?.Message
        })
    };

    await context.Response.WriteAsync(
        JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
}

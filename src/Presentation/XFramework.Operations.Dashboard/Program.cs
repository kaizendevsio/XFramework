using System.Text.Json;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using XFramework.Core.Extensions;
using XFramework.Integration.Extensions;
using XFramework.Operations.Dashboard.Components;
using XFramework.Operations.Dashboard.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddXFrameworkLogging(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
{
    options.DefaultBaseColor = BaseColor.Slate;
    options.DefaultPrimaryColor = PrimaryColor.Blue;
    options.DefaultDarkMode = true;
    options.DetectSystemPreference = true;
    options.DefaultRadius = 0.5;
    options.PersistToLocalStorage = true;
});

builder.Services.Configure<OperationsDashboardOptions>(
    builder.Configuration.GetSection(OperationsDashboardOptions.SectionName));

builder.Services.AddXFrameworkBoltClient(builder.Configuration);
builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Operations.Dashboard");

builder.Services.AddScoped<OperationsRegistryClient>();

builder.Services.AddHttpClient<SeqLogClient>((sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var seqUrl = configuration["Seq:Url"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        client.BaseAddress = new Uri(seqUrl.TrimEnd('/') + "/");
    }

    var apiKey = configuration["Seq:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Seq-ApiKey", apiKey);
    }
});

builder.Services.AddHttpClient<JaegerTraceClient>((sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var jaegerUrl = configuration["Jaeger:QueryUrl"];
    if (!string.IsNullOrWhiteSpace(jaegerUrl))
    {
        client.BaseAddress = new Uri(jaegerUrl.TrimEnd('/') + "/");
    }
});

builder.Services.AddHealthChecks()
    .AddCheck(
        "operations-dashboard-live",
        () => HealthCheckResult.Healthy("Operations Dashboard is running."),
        tags: ["live"])
    .AddCheck<BoltClientHealthCheck>(
        "operations-dashboard-bolt",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

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
app.MapRazorComponents<App>()
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

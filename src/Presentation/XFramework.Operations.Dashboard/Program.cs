using BlazorBlueprint.Components;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
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

builder.Services.AddXFrameworkBoltClient(builder.Configuration, hostEnvironment: builder.Environment);
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

app.MapXFrameworkHealthChecks("XFramework.Operations.Dashboard");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

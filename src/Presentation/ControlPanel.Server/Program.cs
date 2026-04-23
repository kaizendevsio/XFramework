using BlazorBlueprint.Components;
using XFramework.Integration.Extensions;
using IdentityServer.Integration.Drivers;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using ZLogger;
using ControlPanel.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Logging — ZLogger console (lifecycle only) + Seq (everything including Bolt RPC payloads)
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Console: plain text, Warning+ only for framework noise, but show Debug for our code in dev
builder.Logging.AddZLoggerConsole(options =>
{
    options.UsePlainTextFormatter(formatter =>
    {
        formatter.SetPrefixFormatter($"[{0} {1}] ", (in MessageTemplate template, in LogInfo info) =>
            template.Format(info.Timestamp.Local.ToString("HH:mm:ss"), info.LogLevel));
    });
});

// Per-provider console filter: suppress framework noise + Bolt RPC (those go to Seq only)
builder.Logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("System", LogLevel.Warning);
builder.Logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("XFramework.Integration.Drivers.BoltDriver", LogLevel.None);
builder.Logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("Bolt.Client", LogLevel.None);

// Seq: Debug+ for everything — full Bolt RPC payloads, request/response bodies
var seqUrl = builder.Configuration["Seq:Url"] ?? "http://100.75.11.49:5341";
ZLoggerSeqSink.Register(builder.Logging, seqUrl, minimumLevel: LogLevel.Debug);

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

app.MapStaticAssets();
app.MapRazorComponents<ControlPanel.Server.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

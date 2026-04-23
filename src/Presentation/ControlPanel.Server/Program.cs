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

// Console: plain text, only startup/lifecycle — everything else goes to Seq only
builder.Logging.AddZLoggerConsole(options =>
{
    options.UsePlainTextFormatter(formatter =>
    {
        formatter.SetPrefixFormatter($"[{0} {1}] ", (in MessageTemplate template, in LogInfo info) =>
            template.Format(info.Timestamp.Local.ToString("HH:mm:ss"), info.LogLevel));
    });
});

// Console filter: Warning+ baseline, suppress all Bolt/XFramework.Integration noise entirely
builder.Logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>(level => level >= LogLevel.Warning);
builder.Logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("Microsoft.Hosting.Lifetime", LogLevel.Information);
builder.Logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("XFramework.Integration", LogLevel.None);
builder.Logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("Bolt", LogLevel.None);

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

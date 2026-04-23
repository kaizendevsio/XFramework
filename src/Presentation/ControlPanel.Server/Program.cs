using BlazorBlueprint.Components;
using XFramework.Integration.Extensions;
using IdentityServer.Integration.Drivers;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using ZLogger;
using ControlPanel.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Logging — ZLogger: console for lifecycle only, Seq for everything
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddZLoggerConsole(options =>
{
    options.UseJsonFormatter(formatter =>
    {
        formatter.IncludeProperties = IncludeProperties.Timestamp | IncludeProperties.LogLevel | IncludeProperties.Message;
    });
}, configureEnableAnsiEscape: false);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("XFramework.Integration.Drivers.BoltDriver", LogLevel.None); // Console: suppress Bolt RPC noise
builder.Logging.AddFilter("Bolt.Client", LogLevel.None); // Console: suppress Bolt client noise

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

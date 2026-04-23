using BlazorBlueprint.Components;
using XFramework.Integration.Extensions;
using IdentityServer.Integration.Drivers;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;

var builder = WebApplication.CreateBuilder(args);

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

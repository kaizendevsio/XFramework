using BlazorBlueprint.Components;
using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Core.DataContext;

var builder = WebApplication.CreateBuilder(args);

// BlueprintUI
builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
{
    options.DefaultBaseColor = BaseColor.Slate;
    options.DefaultPrimaryColor = PrimaryColor.Blue;
    options.DefaultRadius = 0.75;
});

// Database + IDataContext (ServerDataContext = direct EF Core)
builder.Services.AddDbContext<DbContext, AppDbContext>((_, options) => options
    .UseNpgsql(
        string.IsNullOrEmpty(builder.Configuration["DefaultDatabaseConnection"])
            ? builder.Configuration.GetConnectionString("DefaultDatabaseConnection")
            : builder.Configuration["DefaultDatabaseConnection"],
        npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning)));

builder.Services.AddServerDataContext<AppDbContext>();

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<ControlPanel.Server.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

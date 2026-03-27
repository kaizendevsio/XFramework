using BlazorBlueprint.Components;
using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Core.DataContext;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// BlueprintUI
builder.Services.AddBlazorBlueprintComponents();

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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<ControlPanel.Server.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

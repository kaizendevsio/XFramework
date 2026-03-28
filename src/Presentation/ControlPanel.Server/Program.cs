using BlazorBlueprint.Components;
using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Core.DataContext;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add BlazorBlueprint services
builder.Services.AddBlazorBlueprintComponents();

// Database — register AppDbContext as both DbContext (for IDataContext) and itself (for admin IgnoreQueryFilters)
builder.Services.AddDbContext<AppDbContext>((_, options) => options
    .UseNpgsql(
        string.IsNullOrEmpty(builder.Configuration["DefaultDatabaseConnection"])
            ? builder.Configuration.GetConnectionString("DefaultDatabaseConnection")
            : builder.Configuration["DefaultDatabaseConnection"],
        npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning)));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddServerDataContext<AppDbContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ControlPanel.Server.Services.AdminDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
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

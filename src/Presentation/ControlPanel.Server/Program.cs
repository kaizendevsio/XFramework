using BlazorBlueprint.Components;
using Microsoft.EntityFrameworkCore.Diagnostics;
using XFramework.Core.DataContext;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add BlazorBlueprint services with theme configuration
builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
{
    options.DefaultBaseColor = BaseColor.Slate;
    options.DefaultPrimaryColor = PrimaryColor.Blue;
    options.DefaultDarkMode = true;
    options.DetectSystemPreference = true;
    options.DefaultRadius = 0.5;
    options.PersistToLocalStorage = true;
});

// Database — use DbContextFactory for concurrent-safe access in Blazor Server
var connString = string.IsNullOrEmpty(builder.Configuration["DefaultDatabaseConnection"])
    ? builder.Configuration.GetConnectionString("DefaultDatabaseConnection")
    : builder.Configuration["DefaultDatabaseConnection"];

builder.Services.AddDbContextFactory<AppDbContext>(options => options
    .UseNpgsql(connString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning)));

// Also register scoped AppDbContext (for IDataContext compatibility)
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
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

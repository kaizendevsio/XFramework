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

// Database — AppDbContext has multiple constructors, so we can't use AddDbContextFactory directly.
// Instead: register AddDbContext normally + a custom factory for AdminDbContext.
var connString = string.IsNullOrEmpty(builder.Configuration["DefaultDatabaseConnection"])
    ? builder.Configuration.GetConnectionString("DefaultDatabaseConnection")
    : builder.Configuration["DefaultDatabaseConnection"];

builder.Services.AddDbContext<AppDbContext>((sp, options) => options
    .UseNpgsql(connString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
    .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning)));

builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddServerDataContext<AppDbContext>();
builder.Services.AddHttpContextAccessor();

// Custom factory lambda for AdminDbContext — creates a fresh AppDbContext per call
builder.Services.AddScoped<ControlPanel.Server.Services.AdminDbContextFactory>(sp =>
{
    var httpAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var config = sp.GetRequiredService<IConfiguration>();
    return new ControlPanel.Server.Services.AdminDbContextFactory(connString!, httpAccessor, config);
});
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

using XFramework.Extensions;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Community.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Community");

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.EnsureDatabase<AppDbContext>();
app.MapXFrameworkHealthChecks("Community");

app.Run();
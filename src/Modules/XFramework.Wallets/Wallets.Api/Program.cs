using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Wallets.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Wallets");

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.EnsureDatabase<AppDbContext>();
app.UseCustomRequestsInAssembly<WalletsBaseRequest>();
app.MapXFrameworkHealthChecks("Wallets");

app.Run();
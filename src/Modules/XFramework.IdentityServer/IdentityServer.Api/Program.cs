using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.IdentityServer.Api");
builder.Services.AddXFrameworkHealthChecks<DbContext>(
    builder.Configuration,
    "IdentityServer");

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.EnsureDatabase<DbContext>();
app.UseCustomRequestsInAssembly<IdentityServerBaseRequest>();
app.MapXFrameworkHealthChecks("IdentityServer");

app.Run();

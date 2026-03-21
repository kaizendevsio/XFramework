using FluentValidation;
using IdentityServer.Api.Features;
using IdentityServer.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

// Register AuthService
builder.Services.AddScoped<IAuthService, AuthService>();

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.IdentityServer.Api");
builder.Services.AddXFrameworkHealthChecks<DbContext>(
    builder.Configuration,
    "IdentityServer");

// Workaround: dotnet/aspnetcore#63857 — IdentityServer endpoints reference EF entities
// with circular navigation properties that crash the JsonSchemaExporter.
// Exclude all endpoints from OpenAPI until .NET 11 fixes the schema generator.
builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = _ => false;
});

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.EnsureDatabase<DbContext>();
app.UseCustomRequestsInAssembly<IdentityServerBaseRequest>();
app.MapXFrameworkHealthChecks("IdentityServer");

// API Documentation
app.MapApiDocumentation();

// Map VSA Feature Endpoints
app.MapIdentityServerFeatureEndpoints();

app.Run();
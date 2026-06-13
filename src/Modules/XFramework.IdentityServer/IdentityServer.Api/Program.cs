using FluentValidation;
using IdentityServer.Api.Features.Verification.Confirm;
using IdentityServer.Api.Generated;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);

// Register AuthService
builder.Services.AddScoped<IAuthService, AuthService>();

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Register DataContext handler for entity query/mutation via Bolt
builder.Services.AddDataContextHandler(typeof(Program).Assembly);

// Rate limiting — global 100/min per IP + stricter "auth" and "password-reset" policies
builder.Services.AddXFrameworkRateLimiting();

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
app.UseXFrameworkRateLimiting();
app.EnsureDatabase<DbContext>();
// Bolt handlers are now source-generated from [BoltHandler] on endpoint methods.
// Generated IBoltHandler implementations are auto-registered by
// BoltHandlerRegistrationHostedService at startup.
app.MapXFrameworkHealthChecks("IdentityServer");

// API Documentation
app.MapApiDocumentation();

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
app.MapGeneratedEndpoints();
app.MapConfirmVerificationEndpoint();

app.Run();

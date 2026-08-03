using FluentValidation;
using IdentityServer.Api.Features.Credentials.Update;
using IdentityServer.Api.Features.Verification.Confirm;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Infrastructure;
using IdentityServer.Api.Features.GeneratedEntityValidation;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddIdentityServerRemoteEntityValidation();

// Register DataContext handler for entity query/mutation via Bolt
builder.Services.AddDataContextHandler(typeof(Program).Assembly);

// Rate limiting — global 100/min per IP + stricter "auth" and "password-reset" policies
builder.Services.AddXFrameworkTrustedProxyForwarding(builder.Configuration);
builder.Services.AddXFrameworkRateLimiting();
builder.Services.AddDistributedStrictSecurityRateLimiting(builder.Configuration, builder.Environment);

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.IdentityServer.Api");
builder.Services.AddXFrameworkHealthChecks<DbContext>(
    builder.Configuration,
    "IdentityServer");

var app = (WebApplication)builder.Build();
var serviceIdentityConfiguration = app.Services.GetRequiredService<ServiceIdentityConfiguration>();
if (serviceIdentityConfiguration.BoltTransportTokenIssuerEnabled)
    _ = app.Services.GetRequiredService<IBoltTransportTokenSigner>();

app.UseCorrelationId();
app.UseXFrameworkTrustedProxyForwarding();
app.UseDistributedStrictSecurityRateLimiting();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(IdentityServerFeatureGateRoutes.Configure);
// Bolt handlers are now source-generated from [BoltHandler] on endpoint methods.
// Generated IBoltHandler implementations are auto-registered by
// BoltHandlerRegistrationHostedService at startup.
app.MapXFrameworkHealthChecks("IdentityServer");

// API Documentation
app.MapApiDocumentation();

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
app.MapGeneratedEndpoints();
XFramework.GeneratedEndpoints.GeneratedEntityEndpointRoutes.MapGeneratedEntityEndpoints(app);
app.MapConfirmVerificationEndpoint();
app.MapUpdateCredentialEndpoint();

app.Run();

public partial class Program;

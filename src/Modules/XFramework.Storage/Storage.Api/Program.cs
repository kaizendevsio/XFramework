using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Extensions;
using Storage.Api.Features.Sessions.UploadPart;
using Storage.Api.Generated;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);
builder.Services.AddIdentityServerSessionValidation();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Storage.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Storage");

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddDataContextHandler(typeof(Program).Assembly);
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(options =>
{
    options.RequireFeature(TenantModuleFeatureKeys.Storage, "/api/storage");
    options.RequireFeature(TenantModuleFeatureKeys.Storage, "/api/storage-files");
    options.RequireFeature(TenantModuleFeatureKeys.Storage, "/api/storage-file-types");
});
app.MapXFrameworkHealthChecks("Storage");
var securedStorageEndpoints = app.MapGroup(string.Empty).RequireAuthorization();
securedStorageEndpoints.MapGeneratedEndpoints();
securedStorageEndpoints.MapUploadStorageFilePartRestEndpoint();
app.MapApiDocumentation();

app.Run();

public partial class Program;

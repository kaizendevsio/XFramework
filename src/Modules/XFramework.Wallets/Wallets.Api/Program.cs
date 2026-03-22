using FluentValidation;
using Wallets.Api.Features.Batch.DecrementBatch;
using Wallets.Api.Features.Batch.IncrementBatch;
using Wallets.Api.Features.Batch.TransferBatch;
using Wallets.Api.Features.Wallets.Get;
using Wallets.Api.Features.Wallets.GetByCredential;
using Wallets.Api.Generated;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;

var builder = XApplication.Configure<Program>();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Wallets.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Wallets");

// Workaround: dotnet/aspnetcore#63857 — Wallet endpoints reference EF entities
// with circular navigation properties that crash the JsonSchemaExporter.
// Exclude all endpoints from OpenAPI until .NET 11 fixes the schema generator.
builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = _ => false;
});

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.EnsureDatabase<AppDbContext>();
// StreamFlow handlers are now source-generated from [StreamFlowHandler] on endpoint methods.
// UseCustomRequestsInAssembly is no longer needed — the generated ISignalREventHandler
// implementations are auto-discovered by ScanAndRegisterHandlers() at startup.
app.MapXFrameworkHealthChecks("Wallets");

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
app.MapGeneratedEndpoints();

// Manual endpoints with custom param binding (route params, headers)
GetWalletEndpoint.Map(app);
GetWalletsByCredentialEndpoint.Map(app);

// Batch endpoints (manual — complex IResult returns)
app.MapBatchIncrementEndpoint();
app.MapBatchDecrementEndpoint();
app.MapBatchTransferEndpoint();

app.MapApiDocumentation();

app.Run();
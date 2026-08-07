using FluentValidation;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Extensions;
using Wallets.Api.Features.Batch.DecrementBatch;
using Wallets.Api.Features.Batch.IncrementBatch;
using Wallets.Api.Features.Batch.TransferBatch;
using Wallets.Api.Features.Wallets.Get;
using Wallets.Api.Features.Wallets.GetByCredential;
using Wallets.Api.Generated;
using XFramework.Core.DataContext;
using XFramework.Core.Extensions;
using XFramework.Core.Health;
using XFramework.Core.Middlewares;
using XFramework.Core.RateLimiting;
using XFramework.Integration.Extensions;

var builder = XApplication.Configure<Program>();
builder.Logging.AddXFrameworkLogging(builder.Configuration);
builder.Services.AddIdentityServerSessionValidation();

builder.Services.InstallOpenTelemetry(builder.Configuration, "XFramework.Wallets.Api");
builder.Services.AddXFrameworkHealthChecks<AppDbContext>(
    builder.Configuration,
    "Wallets");

// Workaround: dotnet/aspnetcore#63857 — Wallet endpoints reference EF entities
// with circular navigation properties that crash the JsonSchemaExporter.
// Include only the advanced wallet routes that use DTO request/response contracts.
builder.Services.AddOpenApi("v1", options =>
{
    options.CreateSchemaReferenceId = type => type.Type.Name;
    options.ShouldInclude = api =>
        api.RelativePath?.StartsWith("api/wallets/deposits", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/withdrawals", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/workflows", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/payment-webhooks", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/outbox", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/reconciliation", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/approvals", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/cases", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/policy", StringComparison.OrdinalIgnoreCase) == true ||
        api.RelativePath?.StartsWith("api/wallets/reports", StringComparison.OrdinalIgnoreCase) == true;
});

// Register FluentValidation validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Register DataContext handler for entity query/mutation via Bolt
builder.Services.AddDataContextHandler(typeof(Program).Assembly);
XFramework.GeneratedServices.GeneratedEntityServiceRegistrations
    .AddGeneratedEntityServices(builder.Services);

// Rate limiting — global 100/min per IP
builder.Services.AddXFrameworkRateLimiting();

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.UseXFrameworkRateLimiting();
app.UseTenantModuleFeatureGate(options =>
{
    options.RequireFeature(TenantModuleFeatureKeys.WalletsTransfers, "/api/wallets/transfer");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsTransfers, "/api/wallets/convert");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsDeposits, "/api/wallets/deposits");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsDeposits, "/api/deposit-requests");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsWithdrawals, "/api/wallets/withdrawals");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsWithdrawals, "/api/withdrawal-requests");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsBatch, "/api/wallets/batch");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsReconciliation, "/api/wallets/reconciliation");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsReconciliation, "/api/wallet-reconciliation-items");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsReconciliation, "/api/wallet-reconciliation-runs");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsPolicy, "/api/wallets/approvals");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsPolicy, "/api/wallets/cases");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsPolicy, "/api/wallets/policy");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsPolicy, "/api/wallet-approval-requests");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsPolicy, "/api/wallet-cases");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsPolicy, "/api/wallet-policy-rules");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsPolicy, "/api/wallet-fee-schedules");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsWebhooks, "/api/wallets/payment-webhooks");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsWebhooks, "/api/wallets/outbox");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsWebhooks, "/api/wallet-payment-webhook-events");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsWebhooks, "/api/wallet-outbox-messages");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsReporting, "/api/wallets/reports");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsReporting, "/api/wallet-operations");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsReporting, "/api/wallet-ledger-entries");
    options.RequireFeature(TenantModuleFeatureKeys.WalletsReporting, "/api/wallet-balance-snapshots");
    options.RequireFeature(TenantModuleFeatureKeys.Wallets, "/api/wallets");
    options.RequireFeature(TenantModuleFeatureKeys.Wallets, "/api/wallet-types");
    options.RequireFeature(TenantModuleFeatureKeys.Wallets, "/api/wallet-transfers");
    options.RequireFeature(TenantModuleFeatureKeys.Wallets, "/api/wallet-transactions");
    options.RequireFeature(TenantModuleFeatureKeys.Wallets, "/api/wallet-transaction-line-items");
    options.RequireFeature(TenantModuleFeatureKeys.Wallets, "/api/wallet-addresses");
    options.RequireFeature(TenantModuleFeatureKeys.Wallets, "/api/exchange-rates");
    options.RequireFeature(TenantModuleFeatureKeys.Wallets, "/api/currencies");
});
app.EnsureDatabase<AppDbContext>();
// Bolt handlers are now source-generated from [BoltHandler] on endpoint methods.
// Generated IBoltHandler implementations are auto-registered by
// BoltHandlerRegistrationHostedService at startup.
app.MapXFrameworkHealthChecks("Wallets");

// Map feature endpoints (source-generated from [MapPost/Get/...] attributes)
app.MapGeneratedEndpoints();
XFramework.GeneratedEndpoints.GeneratedEntityEndpointRoutes.MapGeneratedEntityEndpoints(app);

// Manual endpoints with custom param binding (route params, headers)
GetWalletEndpoint.Map(app);
GetWalletsByCredentialEndpoint.Map(app);

// Batch endpoints (manual — complex IResult returns)
app.MapBatchIncrementEndpoint();
app.MapBatchDecrementEndpoint();
app.MapBatchTransferEndpoint();

app.MapApiDocumentation();

app.Run();

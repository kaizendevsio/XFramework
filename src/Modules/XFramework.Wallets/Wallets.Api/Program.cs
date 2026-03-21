using FluentValidation;
using Wallets.Api.Features.Batch;
using Wallets.Api.Features.Wallets;
using Wallets.Api.Features.Wallets.AddFunds;
using Wallets.Api.Features.Wallets.Convert;
using Wallets.Api.Features.Wallets.Create;
using Wallets.Api.Features.Wallets.Transfer;
using Wallets.Api.Features.Wallets.WithdrawFunds;
using Wallets.Domain.Shared.Contracts.Requests;
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

// Register FluentValidation validators
builder.Services.AddScoped<IValidator<CreateWalletRequest>, CreateWalletValidator>();
builder.Services.AddScoped<IValidator<IncrementWalletRequest>, AddFundsValidator>();
builder.Services.AddScoped<IValidator<DecrementWalletRequest>, WithdrawFundsValidator>();
builder.Services.AddScoped<IValidator<TransferWalletRequest>, TransferValidator>();
builder.Services.AddScoped<IValidator<ConvertWalletRequest>, ConvertValidator>();

var app = (WebApplication)builder.Build();

app.UseCorrelationId();
app.EnsureDatabase<AppDbContext>();
app.UseCustomRequestsInAssembly<WalletsBaseRequest>();
app.MapXFrameworkHealthChecks("Wallets");

// Map VSA Feature Endpoints
app.MapWalletEndpoints();
app.MapBatchEndpoints();
app.MapApiDocumentation();

app.Run();
using Payments.Core;
using Wallets.Api.Events;
using Wallets.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Wallets.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
        services.AddPaymentServices(
            configuration["Wallets:Payments:CallbackBaseUrl"]
            ?? configuration["Payments:CallbackBaseUrl"]
            ?? "http://localhost");

        // Register wallet event publisher (singleton — owns the in-memory event buffer)
        services.AddSingleton<IWalletEventPublisher, WalletEventPublisher>();

        // Register wallet services
        services.AddHttpContextAccessor();
        services.AddScoped<IWalletFeatureGateService, WalletFeatureGateService>();
        services.AddScoped<IWalletRequestContextResolver, WalletRequestContextResolver>();
        services.AddScoped<IWalletFeeCalculator, WalletFeeCalculator>();
        services.AddScoped<IWalletPolicyEvaluator, WalletPolicyEvaluator>();
        services.AddScoped<IWalletLedgerService, WalletLedgerService>();
        services.AddScoped<IWalletOperationsService, WalletOperationsService>();
        services.AddScoped<IBatchWalletService, BatchWalletService>();
        services.AddScoped<IWalletWorkflowService, WalletWorkflowService>();
        services.AddScoped<IWalletProviderWorkflowService, WalletWorkflowService>();
        services.AddScoped<IWalletApprovalWorkflowService, WalletWorkflowService>();
        services.AddScoped<IWalletCaseWorkflowService, WalletWorkflowService>();
        services.AddScoped<IWalletReportingService, WalletWorkflowService>();
        services.AddScoped<IWalletPolicyAdminService, WalletPolicyAdminService>();
        services.AddScoped<IWalletPaymentWebhookService, WalletPaymentWebhookService>();
        services.AddScoped<IWalletOutboxService, WalletOutboxService>();
        services.AddScoped<IWalletOutboxPublisher, WalletOutboxPublisher>();
        services.AddScoped<IWalletReconciliationService, WalletReconciliationService>();
    }
}

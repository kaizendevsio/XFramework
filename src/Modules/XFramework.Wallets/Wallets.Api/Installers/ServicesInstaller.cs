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

        // Register wallet event publisher (singleton — owns the in-memory event buffer)
        services.AddSingleton<IWalletEventPublisher, WalletEventPublisher>();

        // Register wallet services
        services.AddScoped<IWalletPolicyEvaluator, WalletPolicyEvaluator>();
        services.AddScoped<IWalletLedgerService, WalletLedgerService>();
        services.AddScoped<IWalletOperationsService, WalletOperationsService>();
        services.AddScoped<IBatchWalletService, BatchWalletService>();
    }
}

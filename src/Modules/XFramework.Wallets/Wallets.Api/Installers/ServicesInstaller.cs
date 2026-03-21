using Wallets.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Wallets.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddTenantService();

        // Register wallet services
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IBatchWalletService, BatchWalletService>();
    }
}
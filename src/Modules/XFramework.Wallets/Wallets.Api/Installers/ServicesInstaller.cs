using IdentityServer.Integration.Drivers;
using Tenant.Integration.Drivers;
using Wallets.Core;
using Wallets.Core.Services;
using XFramework.Core.Extensions;
using XFramework.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace Wallets.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddTenantService();
        services.AddTenantWrapperServices();
        services.AddIdentityServerWrapperServices();
        
        // Register wallet services
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IBatchWalletService, BatchWalletService>();
    }
}
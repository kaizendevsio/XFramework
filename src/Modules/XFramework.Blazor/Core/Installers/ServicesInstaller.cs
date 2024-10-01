using Address.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using Registry.Integration.Drivers;
using Wallets.Integration.Drivers;

namespace XFramework.Blazor.Core.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddIdentityServerWrapperServices();
        services.AddAddressWrapperServices();
        services.AddWalletsWrapperServices();
        services.AddMessagingWrapperServices();
        services.AddRegistryWrapperServices();
    }
}
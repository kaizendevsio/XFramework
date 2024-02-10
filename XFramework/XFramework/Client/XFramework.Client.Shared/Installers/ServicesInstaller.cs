using Address.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Wallets.Integration.Drivers;
using XFramework.Client.Shared.Core.Services.Interface;
using XFramework.Client.Shared.Interfaces;

namespace XFramework.Client.Shared.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IWebAssemblyHostEnvironment webAssemblyHostEnvironment)
    {
        services.AddIdentityServerWrapperServices();
        services.AddAddressWrapperServices();
        services.AddWalletsWrapperServices();
        services.AddMessagingWrapperServices();
    }
}
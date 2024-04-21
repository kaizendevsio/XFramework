using Address.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Registry.Integration.Drivers;
using Wallets.Integration.Drivers;
using XFramework.Blazor.Interfaces;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Services;

namespace XFramework.Blazor.Installers;

public class ServiceWrapperInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IWebAssemblyHostEnvironment webAssemblyHostEnvironment)
    {
        services.AddSingleton<ISignalRService, SignalRService>();
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddSingleton<IIdentityServerServiceWrapper, IdentityServerServiceWrapper>();
        services.AddSingleton<IAddressServiceWrapper, AddressServiceWrapper>();
        services.AddSingleton<IWalletsServiceWrapper, WalletsServiceWrapper>();
        services.AddSingleton<IMessagingServiceWrapper, MessagingServiceWrapper>();
        services.AddSingleton<IRegistryServiceWrapper, RegistryServiceWrapper>(); 
        services.AddSingleton<IHelperService, HelperService>();
    }
}
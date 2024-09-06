using Address.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using Registry.Integration.Drivers;
using Wallets.Integration.Drivers;

namespace XFramework.Blazor.Core.Installers;

public class ServiceWrapperInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
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
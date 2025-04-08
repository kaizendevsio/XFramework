using Address.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Registry.Integration.Drivers;
using Tenant.Integration.Drivers;
using Wallets.Integration.Drivers;

namespace XFramework.Blazor.Core.Installers;

public class ServiceWrapperInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.TryAddSingleton<ISignalRService, SignalRService>();
        services.TryAddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.TryAddSingleton<IIdentityServerServiceWrapper, IdentityServerServiceWrapper>();
        services.TryAddSingleton<IAddressServiceWrapper, AddressServiceWrapper>();
        services.TryAddSingleton<IWalletsServiceWrapper, WalletsServiceWrapper>();
        services.TryAddSingleton<IMessagingServiceWrapper, MessagingServiceWrapper>();
        services.TryAddSingleton<IRegistryServiceWrapper, RegistryServiceWrapper>(); 
        services.TryAddSingleton<ITenantServiceWrapper, TenantServiceWrapper>(); 
        services.TryAddSingleton<IHelperService, HelperService>();
    }
}
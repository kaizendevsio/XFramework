using Wallets.Integration.Drivers;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace Gateway.Installers;

public class WrapperInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        /*services.AddSingleton<ICommunityServiceWrapper, CommunityServiceDriver>();*/
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddSingleton<IWalletsServiceWrapper, WalletsServiceWrapper>();
        services.AddIdentityServerWrapperServices();
        services.AddWalletsWrapperServices();
    }
}
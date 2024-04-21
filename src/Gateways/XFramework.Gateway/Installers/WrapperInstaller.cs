using Wallets.Integration.Drivers;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace XFramework.Gateway.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICommunityServiceWrapper, CommunityServiceDriver>();*/
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddSingleton<IWalletsServiceWrapper, WalletsServiceWrapper>();
        services.AddIdentityServerWrapperServices();
        services.AddWalletsWrapperServices();
    }
}
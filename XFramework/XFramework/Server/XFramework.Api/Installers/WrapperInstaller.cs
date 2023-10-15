using Community.Integration.Drivers;
using Community.Integration.Interfaces;
using Wallets.Integration.Drivers;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace XFramework.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICommunityServiceWrapper, CommunityServiceDriver>();
        services.AddSingleton<IIdentityServiceWrapper, IdentityServerDriver>();
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddSingleton<IWalletServiceWrapper, WalletServiceDriver>();
    }
}
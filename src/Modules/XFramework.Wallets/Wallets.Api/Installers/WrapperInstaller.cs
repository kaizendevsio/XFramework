/*using IdentityServer.Api.SignalR;*/

using IdentityServer.Integration.Drivers;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace Wallets.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        //services.AddSingleton<IMessagingServiceWrapper, MessagingServiceDriver>();
    }
}
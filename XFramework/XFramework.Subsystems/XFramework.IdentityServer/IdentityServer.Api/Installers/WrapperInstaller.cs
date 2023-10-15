/*using IdentityServer.Api.SignalR;*/
using IdentityServer.Integration.Drivers;
using IdentityServer.Integration.Interfaces;
using Messaging.Integration.Drivers;
using Messaging.Integration.Interfaces;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace IdentityServer.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        /*services.AddSingleton<ISignalRService, SignalRWrapper>();*/
        services.AddSingleton<IIdentityServiceWrapper, IdentityServerDriver>();
        services.AddSingleton<IMessagingServiceWrapper, MessagingServiceDriver>();
    }
}
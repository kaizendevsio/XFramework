/*using IdentityServer.Api.SignalR;*/

using Messaging.Integration.Drivers;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace Messaging.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddSingleton<IMessagingServiceWrapper, MessagingServiceWrapper>();
        services.AddMessagingWrapperServices();
    }
}
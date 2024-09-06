/*using IdentityServer.Api.SignalR;*/

using Messaging.Integration.Drivers;
using SmsGateway.Integration.Drivers;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace Messaging.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddSingleton<IMessagingServiceWrapper, MessagingServiceWrapper>();
        services.AddSingleton<ISmsGatewayServiceWrapper, SmsGatewayServiceWrapper>();
    }
}
/*using LoadManna.Integration.Drivers;
using LoadManna.Integration.Interfaces.Wrappers;*/

using Messaging.Integration.Drivers;
using Messaging.Integration.Interfaces;
using SmsGateway.Api.SignalR;
using SmsGateway.Core.Services;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace SmsGateway.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddTransient<ILoggerWrapper, LoggerService>();
        services.AddSingleton<ISignalRService, SignalRWrapper>();
        services.AddSingleton<IMessagingNodeServiceWrapper, MessagingNodeServiceDriver>();
    }
}
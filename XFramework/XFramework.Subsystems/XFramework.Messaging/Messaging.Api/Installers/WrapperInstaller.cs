/*using LoadManna.Integration.Drivers;
using LoadManna.Integration.Interfaces.Wrappers;*/

using Messaging.Api.SignalR;
using Messaging.Core.Services;
using SmsGateway.Integration.Drivers;
using SmsGateway.Integration.Interfaces;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace Messaging.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddTransient<ILoggerWrapper, LoggerService>();
        services.AddSingleton<ISignalRService, SignalRWrapper>();
        services.AddSingleton<ISmsGatewayServiceWrapper, SmsGatewayServiceDriver>();
    }
}
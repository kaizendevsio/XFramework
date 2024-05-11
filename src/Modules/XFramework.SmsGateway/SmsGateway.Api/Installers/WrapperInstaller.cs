using Messaging.Integration.Drivers;
using XFramework.Integration.Abstractions.Wrappers;

namespace SmsGateway.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
    }
}
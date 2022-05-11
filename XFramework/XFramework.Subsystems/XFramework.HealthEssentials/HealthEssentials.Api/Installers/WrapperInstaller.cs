using HealthEssentials.Api.SignalR;
using SmsGateway.Integration.Drivers;
using SmsGateway.Integration.Interfaces;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace HealthEssentials.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddSingleton<ISignalRService, SignalRWrapper>();
        services.AddSingleton<ISmsGatewayServiceWrapper, SmsGatewayServiceDriver>();
    }
}
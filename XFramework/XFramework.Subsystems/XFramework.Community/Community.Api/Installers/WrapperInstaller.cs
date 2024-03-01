/*using IdentityServer.Api.SignalR;*/

using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace Community.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
    }
}
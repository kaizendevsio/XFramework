using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;

namespace Gateway.Installers;

public sealed class WrapperInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
    }
}
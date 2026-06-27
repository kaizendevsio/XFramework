using Communications.Api.HostedService;
using XFramework.Domain.Shared.Interfaces;

namespace Communications.Api.Installers;

public sealed class HostedServiceInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddHostedService<CommunicationsOutboxDispatcherHostedService>();
    }
}

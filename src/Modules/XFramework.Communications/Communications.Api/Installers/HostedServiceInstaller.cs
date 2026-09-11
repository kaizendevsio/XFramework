using Communications.Api.HostedService;
using Communications.Api.Services;
using XFramework.Domain.Shared.Interfaces;

namespace Communications.Api.Installers;

public sealed class HostedServiceInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton<CommunicationsOutboxSignal>();
        services.AddHostedService<CommunicationsOutboxDispatcherHostedService>();
    }
}

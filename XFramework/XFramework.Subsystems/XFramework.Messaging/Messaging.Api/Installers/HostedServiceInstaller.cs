using Messaging.Api.HostedService;

namespace Messaging.Api.Installers;

public class HostedServiceInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<ProcessMonitorHostedService>();
    }
}
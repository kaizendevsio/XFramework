using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Stream.HostedService;

namespace StreamFlow.Stream.Installers;

public class HostedServiceInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<BootService>();
        services.AddHostedService<ProcessMonitorHostedService>();
    }
}
using XFramework.Domain.Shared.Interfaces;

namespace IdentityServer.Api.Installers;

public class HostedServiceInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        //services.AddHostedService<ProcessMonitorHostedService>();
    }
}
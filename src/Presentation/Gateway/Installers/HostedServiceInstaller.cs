namespace Gateway.Installers;

public class HostedServiceInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddHostedService<ProcessMonitorHostedService>();
    }
}
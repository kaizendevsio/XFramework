namespace IdentityServer.Api.Installers;

public sealed class HostedServiceInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        //services.AddHostedService<ProcessMonitorHostedService>();
    }
}
using XFramework.Core.Extensions;

namespace Gateway.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton<ProcessMonitorService>();
        services.AddTenantResolver();
    }
}
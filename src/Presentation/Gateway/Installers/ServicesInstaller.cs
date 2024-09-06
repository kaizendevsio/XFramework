using XFramework.Core.Extensions;

namespace Gateway.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton<ProcessMonitorService>();
        
        services.AddTenantService();
        services.AddIdentityServerWrapperServices();
        services.AddTenantWrapperServices();
    }
}
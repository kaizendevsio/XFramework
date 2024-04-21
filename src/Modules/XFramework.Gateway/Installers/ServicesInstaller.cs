using Tenant.Integration.Drivers;

namespace XFramework.Gateway.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ProcessMonitorService>();
        
        services.AddTenantService();
        services.AddIdentityServerWrapperServices();
        services.AddTenantWrapperServices();
    }
}
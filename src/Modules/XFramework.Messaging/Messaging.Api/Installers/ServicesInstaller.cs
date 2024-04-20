using Messaging.Integration.Drivers;
using Tenant.Integration.Drivers;
using XFramework.Integration.Extensions;

namespace Messaging.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTenantWrapperServices();
        services.AddMessagingWrapperServices();
        services.AddTenantService();

    }
}
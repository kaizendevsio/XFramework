using HealthEssentials.Core;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using Tenant.Integration.Drivers;

namespace HealthEssentials.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        /*services.AddIdentityServerWrapperServices();
        services.AddDecoratorHandlers(typeof(IdentityServerCore).Assembly);*/
        
        services.AddTenantService();
        services.AddHealthEssentialsWrapperServices();
        services.AddIdentityServerWrapperServices();
        services.AddTenantWrapperServices();
        services.AddMessagingWrapperServices();
        
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(HealthEssentialsBaseRequest).Assembly,
            typeof(HealthEssentialsCore).Assembly
        ));
    }
}
using Messaging.Integration.Drivers;

namespace HealthEssentials.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        /*services.AddIdentityServerWrapperServices();
        services.AddDecoratorHandlers(typeof(IdentityServerCore).Assembly);*/
        
        services.AddHealthEssentialsWrapperServices();
        services.AddMessagingWrapperServices();
    }
}
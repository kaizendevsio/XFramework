using IdentityServer.Core;
using Messaging.Integration.Drivers;
using XFramework.Integration.Extensions;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddIdentityServerWrapperServices();
        services.AddMessagingWrapperServices();
        services.AddDecoratorHandlers(typeof(IdentityServerCore).Assembly);
    }
}
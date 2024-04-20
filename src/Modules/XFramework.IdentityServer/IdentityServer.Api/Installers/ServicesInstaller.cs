using IdentityServer.Core;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Messaging.Integration.Drivers;
using Tenant.Integration.Drivers;
using XFramework.Core;
using XFramework.Core.Services;
using XFramework.Integration.Extensions;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddIdentityServerWrapperServices();
        services.AddTenantWrapperServices();
        services.AddMessagingWrapperServices();
        services.AddDecoratorHandlers(typeof(IdentityServerCore).Assembly);
        services.AddTenantService();
        
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(IdentityServerBaseRequest).Assembly,
            typeof(IdentityServerCore).Assembly
        ));
    }
}
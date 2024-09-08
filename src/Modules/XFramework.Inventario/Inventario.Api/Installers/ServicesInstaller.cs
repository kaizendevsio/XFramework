using IdentityServer.Domain.Shared.Contracts.Requests;
using Tenant.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace Inventario.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
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
using IdentityServer.Integration.Drivers;
using Tenant.Integration.Drivers;
using Wallets.Core;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace Wallets.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddTenantService();
        services.AddTenantWrapperServices();
        services.AddIdentityServerWrapperServices();
        services.AddDecoratorHandlers(typeof(WalletsCore).Assembly);
        
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(WalletsBaseRequest).Assembly,
            typeof(WalletsCore).Assembly
        ));
    }
}
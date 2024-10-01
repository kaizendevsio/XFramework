using Tenant.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace Community.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddTenantService();
        services.AddTenantWrapperServices();
        services.AddCommunityWrapperServices();
    }
}
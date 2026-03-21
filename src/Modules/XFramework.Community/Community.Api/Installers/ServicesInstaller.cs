using XFramework.Core.Extensions;
using XFramework.Extensions;
using Community.Api.Services;
using XFramework.Domain.Shared.Interfaces;

namespace Community.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddTenantService();
        
        // Register Community Service (VSA Architecture)
        services.AddScoped<ICommunityService, CommunityService>();
    }
}
using XFramework.Integration.Extensions;

namespace Community.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddCommunityWrapperServices();
    }
}
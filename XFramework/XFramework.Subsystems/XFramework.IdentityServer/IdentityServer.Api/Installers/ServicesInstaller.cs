using IdentityServer.Core.Services;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICachingService, CachingService>();
        services.AddSingleton<IHelperService, HelperService>();
        services.AddSingleton<IJwtService, JwtService>();
    }
}
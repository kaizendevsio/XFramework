using Communications.Integration.Drivers;
using XFramework.Integration.Extensions;

namespace IdentityServer.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration);
        services.AddSingleton<ICommunicationsServiceWrapper, CommunicationsServiceWrapper>();
    }
}
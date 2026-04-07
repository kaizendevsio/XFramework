using XFramework.Integration.Extensions;

namespace Gateway.Installers;

public sealed class WrapperInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration);
    }
}
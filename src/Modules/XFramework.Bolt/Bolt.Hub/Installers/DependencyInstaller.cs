using XFramework.Domain.Shared.Interfaces;

namespace Bolt.Hub.Installers;

public sealed class DependencyInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
    }
}
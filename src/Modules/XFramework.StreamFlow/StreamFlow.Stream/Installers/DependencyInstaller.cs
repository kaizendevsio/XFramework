using XFramework.Domain.Shared.Interfaces;

namespace StreamFlow.Stream.Installers;

public class DependencyInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        
    }
}
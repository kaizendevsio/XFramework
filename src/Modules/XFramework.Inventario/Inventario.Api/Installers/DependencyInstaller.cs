using XFramework.Domain.Shared.Interfaces;

namespace Inventario.Api.Installers;

public class DependencyInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
       services.AddMediatRHandlers();
    }
}
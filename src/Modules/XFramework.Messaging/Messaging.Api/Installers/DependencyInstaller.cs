using XFramework.Domain.Shared.Interfaces;

namespace Messaging.Api.Installers;

public class DependencyInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddMediatRHandlers();
    }
}
using XFramework.Domain.Shared.Interfaces;

namespace Messaging.Api.Installers;

public class DependencyInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // REMOVED: services.AddMediatRHandlers();
        // MediatR removed - VSA architecture uses direct service injection
    }
}
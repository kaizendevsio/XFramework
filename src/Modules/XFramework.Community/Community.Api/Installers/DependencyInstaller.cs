using XFramework.Domain.Shared.Interfaces;

namespace Community.Api.Installers;

public class DependencyInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // MediatR removed - VSA architecture uses direct service injection
        // Add any required service registrations here
    }
}
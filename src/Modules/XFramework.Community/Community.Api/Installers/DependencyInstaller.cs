using XFramework.Domain.Shared.Interfaces;

namespace Community.Api.Installers;

public sealed class DependencyInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // MediatR removed - VSA architecture uses direct service injection
        // Add any required service registrations here
    }
}
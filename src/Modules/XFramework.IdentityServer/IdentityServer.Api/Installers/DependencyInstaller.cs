using XFramework.Domain.Shared.Interfaces;

namespace IdentityServer.Api.Installers;

public sealed class DependencyInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // REMOVED: services.AddMediatRHandlers();
        // MediatR removed - VSA architecture uses direct service injection
    }
}
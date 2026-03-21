using XFramework.Domain.Shared.Interfaces;

namespace SmsGateway.Api.Installers;

public sealed class DependencyInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        //services.AddMediatRHandlers();
    }
}
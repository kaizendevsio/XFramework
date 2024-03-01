namespace HealthEssentials.Api.Installers;

public class DependencyInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatRHandlers();
    }
}
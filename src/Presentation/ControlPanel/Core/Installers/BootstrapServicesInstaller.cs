namespace ControlPanel.Core.Installers;

public class BootstrapServicesInstaller : IInstaller
{ 
    public virtual void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddScoped<IBootstrapService, BootstrapService>();
    }
}
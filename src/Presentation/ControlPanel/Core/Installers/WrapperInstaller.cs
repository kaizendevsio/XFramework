namespace ControlPanel.Core.Installers;

public class WrapperInstaller : IInstaller
{ 
    public virtual void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        // Install base Blazor Services
        services.InstallBlazorBaseServices(configuration, hostEnvironment);
    }
}
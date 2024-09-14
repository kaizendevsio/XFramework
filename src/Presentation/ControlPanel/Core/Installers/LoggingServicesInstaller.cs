namespace ControlPanel.Core.Installers;

public class LoggingServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddSerilog(configuration);
    }
}
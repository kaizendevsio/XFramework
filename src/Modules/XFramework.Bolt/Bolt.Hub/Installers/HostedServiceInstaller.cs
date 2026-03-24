using Bolt.Hub.Services;
using XFramework.Domain.Shared.Interfaces;

namespace Bolt.Hub.Installers;

/// <summary>
/// Installer for Bolt hosted background services.
/// </summary>
public sealed class HostedServiceInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register BoltProcessor as hosted service for background message processing
        // Processes queued messages from channels with proper backpressure and error handling
        services.AddHostedService<BoltProcessor>();
        
        //services.AddHostedService<ProcessMonitorHostedService>();
    }
}
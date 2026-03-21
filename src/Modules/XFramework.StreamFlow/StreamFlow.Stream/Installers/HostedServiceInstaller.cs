using StreamFlow.Stream.Services;
using XFramework.Domain.Shared.Interfaces;

namespace StreamFlow.Stream.Installers;

/// <summary>
/// Installer for StreamFlow hosted background services.
/// </summary>
public sealed class HostedServiceInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register StreamFlowProcessor as hosted service for background message processing
        // Processes queued messages from channels with proper backpressure and error handling
        services.AddHostedService<StreamFlowProcessor>();
        
        //services.AddHostedService<ProcessMonitorHostedService>();
    }
}
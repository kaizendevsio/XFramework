using StreamFlow.Core.Services;
using XFramework.Domain.Shared.Interfaces;
using ICachingService = StreamFlow.Core.Interfaces.ICachingService;

namespace StreamFlow.Stream.Installers;

/// <summary>
/// Installer for StreamFlow core services.
/// Registers message queue and caching services as singletons.
/// </summary>
public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register StreamFlowMessageQueue as singleton for channel-based message queueing
        // Capacity: 10,000 messages with backpressure handling
        services.AddSingleton<StreamFlowMessageQueue>(sp => new StreamFlowMessageQueue(10000));
        
        // Register CachingService as singleton (requires StreamFlowMessageQueue)
        services.AddSingleton<ICachingService, CachingService>();
    }
}
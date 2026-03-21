using StreamFlow.Stream.Interfaces;
using StreamFlow.Stream.Services;
using XFramework.Domain.Shared.Interfaces;

namespace StreamFlow.Stream.Installers;

/// <summary>
/// Installer for StreamFlow core services.
/// Registers message queue, caching, and StreamFlow services.
/// </summary>
public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register StreamFlowMessageQueue as singleton for channel-based message queueing
        // Capacity: 10,000 messages with backpressure handling
        services.AddSingleton<StreamFlowMessageQueue>(sp => new StreamFlowMessageQueue(10000));
        
        // Register CachingService as singleton (requires StreamFlowMessageQueue)
        services.AddSingleton<ICachingService, CachingService>();
        
        // Register StreamFlowService as scoped service (VSA pattern)
        // Replaces MediatR handlers with direct service injection
        services.AddScoped<IStreamFlowService, StreamFlowService>();
    }
}
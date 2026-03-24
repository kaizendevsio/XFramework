using Bolt.Server;
using StreamFlow.Stream.Interfaces;
using StreamFlow.Stream.Services;
using XFramework.Domain.Shared.Configurations;
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
        // Uses QueueDepth from StreamFlowConfiguration if > 0, else defaults to 10,000
        services.AddSingleton<StreamFlowMessageQueue>();

        // Register DeadLetterQueue as singleton for undeliverable messages
        services.AddSingleton<DeadLetterQueue>();

        // Register CachingService as singleton (requires StreamFlowMessageQueue)
        services.AddSingleton<ICachingService, CachingService>();

        // Register StreamFlowService as singleton — _pendingInvocations must be shared
        // across hub connections for the Invoke/InvokeResponse pattern to work
        services.AddSingleton<IStreamFlowService, StreamFlowService>();

        // Thin binary WebSocket server (Phase 3 — runs alongside SignalR)
        services.AddSingleton<BoltServer>();
    }
}
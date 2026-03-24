using Bolt.Server;
using Bolt.Hub.Interfaces;
using Bolt.Hub.Services;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Interfaces;

namespace Bolt.Hub.Installers;

/// <summary>
/// Installer for Bolt core services.
/// Registers message queue, caching, and Bolt services.
/// </summary>
public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register BoltMessageQueue as singleton for channel-based message queueing
        // Uses QueueDepth from BoltConfiguration if > 0, else defaults to 10,000
        services.AddSingleton<BoltMessageQueue>();

        // Register DeadLetterQueue as singleton for undeliverable messages
        services.AddSingleton<DeadLetterQueue>();

        // Register CachingService as singleton (requires BoltMessageQueue)
        services.AddSingleton<ICachingService, CachingService>();

        // Register BoltHubService as singleton — _pendingInvocations must be shared
        // across hub connections for the Invoke/InvokeResponse pattern to work
        services.AddSingleton<IBoltHubService, BoltHubService>();

        // Thin binary WebSocket server (Phase 3 — runs alongside SignalR)
        services.AddSingleton<BoltServer>();
    }
}
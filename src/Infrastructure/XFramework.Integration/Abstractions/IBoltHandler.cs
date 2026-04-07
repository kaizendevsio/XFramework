using Bolt.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace XFramework.Integration.Abstractions;

/// <summary>
/// Marker interface for source-generated Bolt RPC handlers.
/// Each generated handler registers a callback on BoltClient via RegisterHandler
/// for a specific request type. Handlers are auto-discovered and registered at startup.
/// </summary>
public interface IBoltHandler
{
    /// <summary>
    /// Register this handler's callback on the BoltClient. Called once at startup.
    /// </summary>
    void Register(BoltClient client, ILogger logger, IServiceScopeFactory scopeFactory);
}

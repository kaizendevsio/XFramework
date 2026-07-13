using System.Net;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Bolt.Hub.Security;
using Bolt.Server;
using MemoryPack;

namespace Bolt.Hub.Services;

public sealed class BoltServiceDiscoveryHostedService(
    BoltServer server,
    IServiceScopeFactory scopeFactory,
    ILogger<BoltServiceDiscoveryHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Bolt service discovery presence is single-instance only. Do not horizontally scale Bolt Hub service discovery until instance-scoped leases are implemented.");

        using (var scope = scopeFactory.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
            await registry.ResetPresenceAsync(cancellationToken);
        }

        server.RegisterHandler(
            BoltServiceDiscoveryCommands.AdvertiseServiceManifest,
            HandleAdvertiseServiceManifestAsync);
        server.RegisterHandler(
            BoltServiceDiscoveryCommands.GetServiceRegistry,
            HandleGetServiceRegistryAsync);
        server.RegisterHandler(
            BoltServiceDiscoveryCommands.GetModuleRegistry,
            HandleGetModuleRegistryAsync);

        server.ClientRegistered += HandleClientRegisteredAsync;
        server.ClientDisconnected += HandleClientDisconnectedAsync;

        logger.LogInformation("Bolt service discovery registry handlers registered");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        server.ClientRegistered -= HandleClientRegisteredAsync;
        server.ClientDisconnected -= HandleClientDisconnectedAsync;
        return Task.CompletedTask;
    }

    private async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HandleAdvertiseServiceManifestAsync(
        BoltRequestContext context,
        ReadOnlyMemory<byte> payload,
        Guid requestId)
    {
        var manifest = MemoryPackSerializer.Deserialize<BoltServiceManifest>(payload.Span);
        if (manifest is null)
        {
            return Serialize(HttpStatusCode.BadRequest, new BoltServiceManifestAdvertisementResponse
            {
                Accepted = false,
                Message = "Manifest payload is required."
            });
        }

        using var scope = scopeFactory.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        var response = await registry.AdvertiseAsync(context, manifest, CancellationToken.None);

        return Serialize(response.Accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response);
    }

    private async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HandleGetServiceRegistryAsync(
        BoltRequestContext context,
        ReadOnlyMemory<byte> payload,
        Guid requestId)
    {
        if (!BoltAuthorizationPolicies.IsServiceDiscoveryReader(context.User))
        {
            logger.LogWarning(
                "Rejected Bolt-local service registry read. client={ClientId} connection={ConnectionId}",
                context.ClientId,
                context.ConnectionId);
            return (HttpStatusCode.Forbidden, ReadOnlyMemory<byte>.Empty);
        }

        var request = payload.IsEmpty
            ? new BoltServiceRegistryRequest()
            : MemoryPackSerializer.Deserialize<BoltServiceRegistryRequest>(payload.Span) ?? new BoltServiceRegistryRequest();

        using var scope = scopeFactory.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        var response = await registry.GetServicesAsync(request, CancellationToken.None);

        return Serialize(HttpStatusCode.OK, response);
    }

    private async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HandleGetModuleRegistryAsync(
        BoltRequestContext context,
        ReadOnlyMemory<byte> payload,
        Guid requestId)
    {
        if (!BoltAuthorizationPolicies.IsServiceDiscoveryReader(context.User))
        {
            logger.LogWarning(
                "Rejected Bolt-local module registry read. client={ClientId} connection={ConnectionId}",
                context.ClientId,
                context.ConnectionId);
            return (HttpStatusCode.Forbidden, ReadOnlyMemory<byte>.Empty);
        }

        var request = payload.IsEmpty
            ? new BoltModuleRegistryRequest()
            : MemoryPackSerializer.Deserialize<BoltModuleRegistryRequest>(payload.Span) ?? new BoltModuleRegistryRequest();

        using var scope = scopeFactory.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        var response = await registry.GetModulesAsync(request, CancellationToken.None);

        return Serialize(HttpStatusCode.OK, response);
    }

    private async Task HandleClientRegisteredAsync(BoltClientConnectionEvent connectionEvent, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        await registry.MarkConnectedAsync(connectionEvent, ct);
    }

    private async Task HandleClientDisconnectedAsync(BoltClientConnectionEvent connectionEvent, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        await registry.MarkDisconnectedAsync(connectionEvent, ct);
    }

    private static (HttpStatusCode, ReadOnlyMemory<byte>) Serialize<T>(HttpStatusCode statusCode, T response) =>
        (statusCode, MemoryPackSerializer.Serialize(response));
}

using System.Net;
using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Bolt.Hub.Security;
using Bolt.Server;
using MemoryPack;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Bolt.Hub.Services;

public sealed class BoltServiceDiscoveryHostedService(
    BoltServer server,
    IServiceScopeFactory scopeFactory,
    ILogger<BoltServiceDiscoveryHostedService> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Bolt service discovery presence is single-instance only. Do not horizontally scale Bolt Hub service discovery until instance-scoped leases are implemented.");

        using (var scope = scopeFactory.CreateScope())
        {
            if (!await AuthorizeRegistryScopeAsync(scope.ServiceProvider, Guid.NewGuid(), cancellationToken))
                throw new InvalidOperationException("Bolt service discovery could not establish its trusted service context.");

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
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = scopeFactory.CreateScope();
                if (!await AuthorizeRegistryScopeAsync(scope.ServiceProvider, Guid.NewGuid(), stoppingToken))
                    continue;

                var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
                await registry.RetireStaleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        server.ClientRegistered -= HandleClientRegisteredAsync;
        server.ClientDisconnected -= HandleClientDisconnectedAsync;
        await base.StopAsync(cancellationToken);
    }

    private async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HandleAdvertiseServiceManifestAsync(
        BoltRequestContext context,
        ReadOnlyMemory<byte> payload,
        Guid requestId,
        CancellationToken ct)
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
        if (!await AuthorizeRegistryScopeAsync(scope.ServiceProvider, requestId, ct))
            return (HttpStatusCode.ServiceUnavailable, ReadOnlyMemory<byte>.Empty);

        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        var response = await registry.AdvertiseAsync(context, manifest, ct);

        return Serialize(response.Accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response);
    }

    private async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HandleGetServiceRegistryAsync(
        BoltRequestContext context,
        ReadOnlyMemory<byte> payload,
        Guid requestId,
        CancellationToken ct)
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
        if (!await AuthorizeRegistryScopeAsync(scope.ServiceProvider, requestId, ct))
            return (HttpStatusCode.ServiceUnavailable, ReadOnlyMemory<byte>.Empty);

        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        var response = await registry.GetServicesAsync(request, ct);

        return Serialize(HttpStatusCode.OK, response);
    }

    private async Task<(HttpStatusCode, ReadOnlyMemory<byte>)> HandleGetModuleRegistryAsync(
        BoltRequestContext context,
        ReadOnlyMemory<byte> payload,
        Guid requestId,
        CancellationToken ct)
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
        if (!await AuthorizeRegistryScopeAsync(scope.ServiceProvider, requestId, ct))
            return (HttpStatusCode.ServiceUnavailable, ReadOnlyMemory<byte>.Empty);

        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        var response = await registry.GetModulesAsync(request, ct);

        return Serialize(HttpStatusCode.OK, response);
    }

    private async Task HandleClientRegisteredAsync(BoltClientConnectionEvent connectionEvent, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        if (!await AuthorizeRegistryScopeAsync(scope.ServiceProvider, Guid.NewGuid(), ct))
            return;

        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        await registry.MarkConnectedAsync(connectionEvent, ct);
    }

    private async Task HandleClientDisconnectedAsync(BoltClientConnectionEvent connectionEvent, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        if (!await AuthorizeRegistryScopeAsync(scope.ServiceProvider, Guid.NewGuid(), ct))
            return;

        var registry = scope.ServiceProvider.GetRequiredService<IBoltServiceDiscoveryRegistry>();
        await registry.MarkDisconnectedAsync(connectionEvent, ct);
    }

    private async Task<bool> AuthorizeRegistryScopeAsync(
        IServiceProvider serviceProvider,
        Guid correlationId,
        CancellationToken ct)
    {
        var authorization = await serviceProvider
            .GetRequiredService<ITrustedServiceTargetContextInitializer>()
            .EstablishTenantlessAsync(
                XFrameworkServiceNames.BoltHub,
                [XFrameworkServiceScopes.BoltService],
                XFrameworkServiceNames.BoltHub,
                correlationId,
                ct);
        if (authorization.IsSuccess)
            return true;

        logger.LogError(
            "Bolt service discovery trusted-context authorization failed: {Error}",
            authorization.Error);
        return false;
    }

    private static (HttpStatusCode, ReadOnlyMemory<byte>) Serialize<T>(HttpStatusCode statusCode, T response) =>
        (statusCode, MemoryPackSerializer.Serialize(response));
}

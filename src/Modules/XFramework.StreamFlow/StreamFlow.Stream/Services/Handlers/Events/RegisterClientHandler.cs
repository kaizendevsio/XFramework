using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Stream.Hubs;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Shared.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events;

/// <summary>
/// Handles client registration for StreamFlow connections.
/// Uses atomic counter for client key generation instead of random numbers with goto retry.
/// </summary>
public class RegisterClientHandler(
        ICachingService cachingService,
        IHubContext<MessageQueueHub> hubContext,
        StreamFlowConfiguration streamFlowConfiguration,
        ILogger<RegisterClientHandler> logger)
    : IRequestHandler<RegisterClientCmd, CmdResponse<RegisterClientCmd>>
{
    private static long _clientKeyCounter = 100000000;

    public async Task<CmdResponse<RegisterClientCmd>> Handle(RegisterClientCmd request, CancellationToken cancellationToken)
    {
        var clientKey = GenerateUniqueClientKey();
        var clientInfo = new StreamFlowClient
        {
            StreamId = request.Context.ConnectionId,
            Id = request.Client.Id,
            Name = request.Client.Name
        };

        // Add client with retry logic (replacing goto)
        const int maxAttempts = 100;
        int attempts = 0;
        bool added = false;

        while (attempts < maxAttempts && !added)
        {
            added = cachingService.Clients.TryAdd((int)clientKey, clientInfo);
            if (!added)
            {
                clientKey = GenerateUniqueClientKey();
                attempts++;
            }
        }

        if (!added)
        {
            logger.LogError(
                "Failed to register client after {MaxAttempts} attempts. ConnectionId: {ConnectionId}, ClientId: {ClientId}",
                maxAttempts, request.Context.ConnectionId, request.Client.Id);
            return new()
            {
                HttpStatusCode = HttpStatusCode.InternalServerError
            };
        }

        RememberClient(request);

        var transportType = request.Context.Features.Get<IHttpTransportFeature>()?.TransportType.ToString() ?? "Unknown";
        logger.LogInformation(
            "Client registered. ConnectionId: {ConnectionId}, ClientId: {ClientId}, Transport: {TransportType}, Name: {ClientName}",
            request.Context.ConnectionId, request.Client.Id, transportType, request.Client.Name);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }

    /// <summary>
    /// Generates a unique client key using atomic increment.
    /// More efficient and reliable than random number generation with retry.
    /// </summary>
    private static long GenerateUniqueClientKey()
    {
        return Interlocked.Increment(ref _clientKeyCounter);
    }

    /// <summary>
    /// Remembers client in the absolute clients collection for reconnection tracking.
    /// </summary>
    private void RememberClient(RegisterClientCmd request)
    {
        if (cachingService.AbsoluteClients.All(i => i.Value.Id != request.Client.Id))
        {
            // New client - add to absolute clients
            int attempts = 0;
            const int maxAttempts = 100;
            bool added = false;

            while (attempts < maxAttempts && !added)
            {
                added = cachingService.AbsoluteClients.TryAdd(
                    cachingService.AbsoluteClients.Count,
                    new StreamFlowClient
                    {
                        StreamId = request.Context.ConnectionId,
                        Id = request.Client.Id,
                        Name = request.Client.Name
                    });
                attempts++;
            }

            if (added)
            {
                logger.LogDebug("Client added to absolute clients. ClientId: {ClientId}", request.Client.Id);
            }
            else
            {
                logger.LogWarning(
                    "Failed to add client to absolute clients after {MaxAttempts} attempts. ClientId: {ClientId}",
                    maxAttempts, request.Client.Id);
            }
        }
        else
        {
            // Existing client reconnecting - update connection ID
            var client = cachingService.AbsoluteClients.FirstOrDefault(i => i.Value.Id == request.Client.Id);
            if (client.Value != null)
            {
                client.Value.StreamId = request.Context.ConnectionId;
                logger.LogDebug("Updated connection ID for existing client. ClientId: {ClientId}", request.Client.Id);
            }
        }
    }
}
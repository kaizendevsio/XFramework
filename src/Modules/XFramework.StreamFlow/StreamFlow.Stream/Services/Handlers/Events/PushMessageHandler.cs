using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Domain.Shared.Enums;
using StreamFlow.Stream.Hubs;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Shared.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events;

/// <summary>
/// Handles pushing messages to StreamFlow clients via SignalR.
/// Optimized with Channel-based queueing for better throughput and backpressure handling.
/// </summary>
public class PushMessageHandler(
        ICachingService cachingService,
        IHubContext<MessageQueueHub> hubContext,
        StreamFlowConfiguration streamFlowConfiguration,
        ILogger<PushMessageHandler> logger)
    : IRequestHandler<PushMessageCmd, CmdResponse<PushMessageCmd>>
{
    public async Task<CmdResponse<PushMessageCmd>> Handle(PushMessageCmd request, CancellationToken cancellationToken)
    {
        // Check if Client is Registered
        var client = cachingService.Clients.FirstOrDefault(x => x.Value.StreamId == request.Context.ConnectionId);
        if (client.Value == null)
        {
            logger.LogWarning("Unknown or unauthorized client detected. ConnectionId: {ConnectionId}",
                request.Context.ConnectionId);
            await hubContext.Clients.Client(request.Context.ConnectionId)
                .SendAsync("TelemetryCall", "Client Unknown or Unauthorized", cancellationToken);
            return new()
            {
                HttpStatusCode = HttpStatusCode.Forbidden
            };
        }

        request.RequestMetadata = new()
        {
            RequestId = request.Message.RequestId,
            Name = client.Value.Name
        };
            
        // Execute Sending Message
        switch (request.Message.ExchangeType)
        {
            case MessageExchangeType.FanOut:
                await hubContext.Clients.All.SendAsync(request.Message.CommandName, request.Message,
                    cancellationToken: cancellationToken);
                logger.LogInformation("FanOut message sent. RequestId: {RequestId}, Sender: {SenderName}",
                    request.Message.RequestId, client.Value.Name);
                break;
                
            case MessageExchangeType.Direct:
                await HandleDirectMessageAsync(request, client.Value, cancellationToken);
                break;
                
            case MessageExchangeType.Topic:
                await hubContext.Clients.Group(request.Message.Topic)
                    .SendAsync(request.Message.CommandName, request.Message, cancellationToken: cancellationToken);
                logger.LogInformation("Topic message sent. RequestId: {RequestId}, Topic: {Topic}, Sender: {SenderName}",
                    request.Message.RequestId, request.Message.Topic, client.Value.Name);
                break;
                
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Message.ExchangeType),
                    $"Unsupported exchange type: {request.Message.ExchangeType}");
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }

    /// <summary>
    /// Handles direct message delivery with load balancing and queueing.
    /// </summary>
    private async Task HandleDirectMessageAsync(
        PushMessageCmd request,
        StreamFlowClient sender,
        CancellationToken cancellationToken)
    {
        var availableClients = cachingService.Clients
            .Where(x => x.Value.Id == request.Message.RecipientId)
            .Select(i => i.Value)
            .ToList();
        var count = availableClients.Count;

        StreamFlowClient currentClient = null;

        if (count > 1)
        {
            // Multiple clients available - use round-robin load balancing
            currentClient = SelectClientForLoadBalancing(availableClients, request.Message.RecipientId);
        }
        else if (count == 1)
        {
            currentClient = availableClients.First();
        }

        if (currentClient != null)
        {
            // Client is online, deliver immediately
            logger.LogInformation(
                "Direct message sent. ExchangeType: {ExchangeType}, RequestId: {RequestId}, Sender: {SenderName} -> Recipient: {RecipientName}, Status: {StatusCode}",
                request.Message.ExchangeType, request.Message.RequestId, sender.Name, currentClient.Name, request.Message.ResponseStatusCode);
                
            await hubContext.Clients.Client(currentClient.StreamId)
                .SendAsync(request.Message.CommandName, request.Message, cancellationToken);
            return;
        }

        // Client is not online - check if known and queue if enabled
        if (cachingService.AbsoluteClients.All(x => x.Value.Id != request.Message.RecipientId))
        {
            logger.LogWarning(
                "Invalid recipient for message. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                request.Message.RequestId, sender.Name, request.Message.RecipientId);
            return;
        }

        if (!streamFlowConfiguration.QueueMessages)
        {
            logger.LogInformation(
                "Message queueing disabled. Message dropped. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                request.Message.RequestId, sender.Name, request.Message.RecipientId);
            return;
        }

        // Queue the message using channels with backpressure
        try
        {
            var queued = await cachingService.MessageQueue.TryEnqueueMessageAsync(request.Message, cancellationToken);
            
            if (queued)
            {
                logger.LogInformation(
                    "Message queued for offline recipient. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                    request.Message.RequestId, sender.Name, request.Message.RecipientId);
            }
            else
            {
                logger.LogWarning(
                    "Failed to queue message (channel closed). RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                    request.Message.RequestId, sender.Name, request.Message.RecipientId);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(
                "Message queueing cancelled. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                request.Message.RequestId, sender.Name, request.Message.RecipientId);
        }
    }

    /// <summary>
    /// Selects a client for load balancing using round-robin strategy.
    /// Replaces goto-based retry logic with proper while loops.
    /// </summary>
    private StreamFlowClient SelectClientForLoadBalancing(List<StreamFlowClient> availableClients, string recipientId)
    {
        var count = availableClients.Count;
        var cachedClient = cachingService.LatestClients
            .Select(i => i.Value)
            .FirstOrDefault(x => x.Id == recipientId);

        StreamFlowClient selectedClient;

        if (cachedClient is null)
        {
            // No cached client - use first available
            selectedClient = availableClients[0];
            
            // Add to cache with retry (replacing goto)
            int attempts = 0;
            const int maxAttempts = 100;
            while (attempts < maxAttempts)
            {
                if (cachingService.LatestClients.TryAdd(cachingService.LatestClients.Count, selectedClient))
                {
                    break;
                }
                attempts++;
            }
            
            if (attempts >= maxAttempts)
            {
                logger.LogWarning("Failed to cache latest client after {MaxAttempts} attempts. ClientId: {ClientId}",
                    maxAttempts, selectedClient.Id);
            }
        }
        else
        {
            // Select next client in round-robin fashion
            var cachedClientIndex = availableClients.IndexOf(cachedClient);
            selectedClient = (cachedClientIndex + 1) >= count
                ? availableClients[0]
                : availableClients[cachedClientIndex + 1];

            // Remove old cache entry with retry (replacing goto)
            var tmpIndex = cachingService.LatestClients.FirstOrDefault(i => i.Value.Id == cachedClient.Id);
            if (tmpIndex.Key != 0 || tmpIndex.Value != null)
            {
                int removeAttempts = 0;
                const int maxRemoveAttempts = 100;
                while (removeAttempts < maxRemoveAttempts)
                {
                    if (cachingService.LatestClients.TryRemove(tmpIndex.Key, out _))
                    {
                        break;
                    }
                    removeAttempts++;
                }
            }

            // Add new cache entry with retry (replacing goto)
            int addAttempts = 0;
            const int maxAddAttempts = 100;
            while (addAttempts < maxAddAttempts)
            {
                if (cachingService.LatestClients.TryAdd(0, selectedClient))
                {
                    break;
                }
                addAttempts++;
            }
            
            if (addAttempts >= maxAddAttempts)
            {
                logger.LogWarning("Failed to update latest client cache after {MaxAttempts} attempts. ClientId: {ClientId}",
                    maxAddAttempts, selectedClient.Id);
            }
        }

        return selectedClient;
    }
}
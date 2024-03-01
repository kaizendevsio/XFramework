using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Enums;
using StreamFlow.Stream.Hubs;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events;

public class PushMessageHandler(
        ICachingService cachingService,
        IHubContext<MessageQueueHub> hubContext,
        StreamFlowConfiguration streamFlowConfiguration)
    : IRequestHandler<PushMessageCmd, CmdResponse<PushMessageCmd>>
{
    public async Task<CmdResponse<PushMessageCmd>> Handle(PushMessageCmd request, CancellationToken cancellationToken)
    {
        // Check if Client is Registered
        var clients = cachingService.Clients.ToList();
        var client = cachingService.Clients.FirstOrDefault(x => x.Value.StreamId == request.Context.ConnectionId);
        if (client.Value == null)
        {
            Console.WriteLine($"Unknown or unauthorized client detected");
            await hubContext.Clients.Client(request.Context.ConnectionId).SendAsync("TelemetryCall","Client Unknown or Unauthorized");
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
                await hubContext.Clients.All.SendAsync(request.Message.CommandName, request.Message, cancellationToken: cancellationToken);
                break;
            case MessageExchangeType.Direct:
                StreamFlowClient currentClient;

                var availableClients = cachingService.Clients.Where(x => x.Value.Id == request.Message.RecipientId).Select(i => i.Value).ToList();
                var count = availableClients.Count;
                    
                if (count > 1)
                {
                    var cachedClient = cachingService.LatestClients.Select(i => i.Value).FirstOrDefault(x => x.Id == request.Message.RecipientId);
                    if (cachedClient is null)
                    {
                        var cc = availableClients[0];
                        currentClient = cc;
                            
                        ReTryAddLatestClients:
                        if (!cachingService.LatestClients.TryAdd(cachingService.LatestClients.Count, currentClient))
                        {
                            goto ReTryAddLatestClients;
                        }
                    }
                    else
                    {
                        var cachedClientIndex = availableClients.IndexOf(cachedClient);
                        currentClient = (cachedClientIndex + 1) >= count 
                            ? availableClients[0]
                            : availableClients[cachedClientIndex + 1];
                            
                        ReTryRemoveLatestClients:
                        var tmpIndex = cachingService.LatestClients.FirstOrDefault(i => i.Value.Id == cachedClient.Id);
                        if (!cachingService.LatestClients.TryRemove(tmpIndex.Key, out _))
                        {
                            goto ReTryRemoveLatestClients;
                        }
                                
                        ReTryAddLatestClients:
                        if (!cachingService.LatestClients.TryAdd(0, currentClient))
                        {
                            goto ReTryAddLatestClients;
                        }
                    }
                }
                else
                {
                    currentClient = availableClients.FirstOrDefault();
                }

                if (currentClient != null)
                {
                    Console.WriteLine($"Action: {request.Message.ExchangeType} | Request ID: {request.Message.RequestId} | {request.RequestMetadata.Name} -> {currentClient.Name} ({request.Message.ResponseStatusCode})");
                    await hubContext.Clients.Client(currentClient.StreamId).SendAsync(request.Message.CommandName, request.Message, cancellationToken);
                    break;
                }

                if (cachingService.AbsoluteClients.All(x => x.Value.Id != request.Message.RecipientId))
                {
                    Console.WriteLine($"Connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} has invalid recipient");
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.NotFound
                    };
                }

                if (!streamFlowConfiguration.QueueMessages)
                {
                    Console.WriteLine($"[Message Queue Disabled]; Message from connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} has been dropped; Recipient unavailable");
                    break;
                }

                if (cachingService.QueuedMessages.Where(i => i.Value.RecipientId == request.Message.RecipientId).Count() > streamFlowConfiguration.QueueDepth)
                {
                    Console.WriteLine($"Message from connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} cannot be queued: Queue depth has been exhausted");
                    break;
                }
                    
                cachingService.QueuedMessages.TryAdd(Guid.NewGuid(), request.Message);
                Console.WriteLine($"Message from connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} has been queued; Recipient unavailable");
                   
                break;
            case MessageExchangeType.Topic:
                await hubContext.Clients.Group(request.Message.Topic).SendAsync(request.Message.CommandName, request.Message, cancellationToken: cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}
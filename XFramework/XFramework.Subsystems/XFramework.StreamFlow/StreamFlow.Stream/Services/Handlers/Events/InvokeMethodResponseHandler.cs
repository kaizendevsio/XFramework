using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Stream.Hubs;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events;

public class InvokeMethodResponseHandler(
        ICachingService cachingService,
        IHubContext<MessageQueueHub> hubContext,
        ILogger<InvokeMethodResponseHandler> logger,
        StreamFlowConfiguration streamFlowConfiguration)
    : IRequestHandler<InvokeMethodResponseCmd, CmdResponse<InvokeMethodResponseCmd>>
{
    public async Task<CmdResponse<InvokeMethodResponseCmd>> Handle(InvokeMethodResponseCmd request, CancellationToken cancellationToken)
    {
        // Check if Client is Registered
        var client = cachingService.Clients.FirstOrDefault(x => x.Value.StreamId == request.Context.ConnectionId);
        if (client.Value == null)
        {
            logger.LogInformation($"Unknown or unauthorized client detected");
            await hubContext.Clients.Client(request.Context.ConnectionId).SendAsync("TelemetryCall","Client Unknown or Unauthorized");
            return new()
            {
                HttpStatusCode = HttpStatusCode.Forbidden
            };
        }

        request.RequestMetadata = new()
        {
            RequestId = client.Value.Guid,
            Name = client.Value.Name
        };

        var telemetry = new StreamFlowTelemetry
        {
            ClientGuid = client.Value.Guid,
        };
            
        var c = cachingService.Clients.FirstOrDefault(x => x.Value.Guid == request.MessageQueue.RecipientId);

        if (c.Value != null)
        {
            if (cachingService.PendingMethodCalls.TryRemove(request.MessageQueue.RequestId, out TaskCompletionSource<StreamFlowMessage > methodCallCompletionSource))
            {
                await Task.Run(() => methodCallCompletionSource.SetResult(request.MessageQueue), cancellationToken);
            }

            Console.WriteLine("Response for Invoked Method Received");
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
            
        if (cachingService.AbsoluteClients.All(x => x.Value.Guid != request.MessageQueue.RecipientId))
        {
            Console.WriteLine($"Connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} has invalid recipient");
            goto returnYield;
        }

        if (!streamFlowConfiguration.QueueMessages)
        {
            Console.WriteLine($"[Message Queue Disabled]; Message from connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} has been dropped; Recipient unavailable");
            goto returnYield;
        }

        if (cachingService.QueuedMessages.Where(i => i.Value.RecipientId == request.MessageQueue.RecipientId).Count() > streamFlowConfiguration.QueueDepth)
        {
            Console.WriteLine($"Message from connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} cannot be queued: Queue depth has been exhausted");
            goto returnYield;
        }
                    
        cachingService.QueuedMessages.TryAdd(Guid.NewGuid() , request.MessageQueue);
        Console.WriteLine($"Message from connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} has been queued; Recipient unavailable");

        returnYield:

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
}
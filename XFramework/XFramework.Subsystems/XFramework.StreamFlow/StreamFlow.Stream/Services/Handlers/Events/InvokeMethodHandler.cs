using System.Net;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Stream.Hubs;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.Configurations;
using XFramework.Integration.Services.Helpers;

namespace StreamFlow.Stream.Services.Handlers.Events;

public class InvokeMethodHandler(
        ICachingService cachingService,
        IHubContext<MessageQueueHub> hubContext,
        StreamFlowConfiguration streamFlowConfiguration,
        MetricsMonitor metricsMonitor,
        IHelperService helperService)
    : IRequestHandler<InvokeMethodQuery, QueryResponse<StreamFlowInvokeResponse>>
{
    public async Task<QueryResponse<StreamFlowInvokeResponse>> Handle(InvokeMethodQuery request, CancellationToken cancellationToken)
    {
        // Check if Client is Registered
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
            RequestId = client.Value.Guid,
            Name = client.Value.Name
        };

        var telemetry = new StreamFlowTelemetry
        {
            ClientGuid = client.Value.Guid
        };
            
        var c = cachingService.Clients.FirstOrDefault(x => x.Value.Guid == request.MessageQueue.RecipientId);

        if (c.Value != null)
        {
            using var metricLogger = metricsMonitor.Start($"Invoking method '{request.MessageQueue.CommandName}'");
            
            var methodCallCompletionSource = new TaskCompletionSource<StreamFlowMessage >();
            //methodCallCompletionSource.RunContinuationsAsynchronously();
            if (!cachingService.PendingMethodCalls.TryAdd(request.MessageQueue.RequestId,methodCallCompletionSource))
            {
                metricLogger.Failed($"Error while invoking method '{request.MessageQueue.CommandName}' on {request.MessageQueue.RecipientId}");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Error while invoking method '{request.MessageQueue.CommandName}' on {request.MessageQueue.RecipientId}"
                };
            }
                
            await hubContext.Clients.Client(c.Value.StreamId).SendAsync(request.MessageQueue.CommandName, request.MessageQueue.Data, request.MessageQueue.Message, telemetry, cancellationToken);
            var response = await methodCallCompletionSource.Task.ConfigureAwait(false);

            metricLogger.Completed($"Invoked method '{request.MessageQueue.CommandName}'");
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = new ()
                {
                    HttpStatusCode = response.Adapt<CmdResponse>().HttpStatusCode,
                    Response = response.Data
                }
            };

        }

        if (cachingService.AbsoluteClients.All(x => x.Value.Guid != request.MessageQueue.RecipientId))
        {
            Console.WriteLine($"Connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} has invalid recipient");
            return new()
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                Response = new()
                {
                    HttpStatusCode = HttpStatusCode.NotFound
                }
            };
        }


        Console.WriteLine($"Message from connection with ID {request.RequestMetadata.RequestId} : {request.RequestMetadata.Name} has been dropped; Recipient unavailable");
        return new()
        {
            HttpStatusCode = HttpStatusCode.ServiceUnavailable,
            Response = new()
            {
                HttpStatusCode = HttpStatusCode.NotFound
            }
        };
    }
}
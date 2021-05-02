using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Generic.Contracts.Responses;
using StreamFlow.Stream.Hubs.V1;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events
{
    public class InvokeMethodResponseHandler : CommandBaseHandler, IRequestHandler<InvokeMethodResponseCmd, CmdResponseBO<InvokeMethodResponseCmd>>
    {
        public InvokeMethodResponseHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext, StreamFlowConfiguration streamFlowConfiguration)
        {
            _cachingService = cachingService;
            _hubContext = hubContext;
            _streamFlowConfiguration = streamFlowConfiguration;
        }


        public async Task<CmdResponseBO<InvokeMethodResponseCmd>> Handle(InvokeMethodResponseCmd request, CancellationToken cancellationToken)
        {
            // Check if Client is Registered
            var client = _cachingService.Clients.FirstOrDefault(x => x.StreamId == request.Context.ConnectionId);
            if (client == null)
            {
                Console.WriteLine($"Unknown or unauthorized client detected");
                _hubContext.Clients.Client(request.Context.ConnectionId).SendAsync("TelemetryCall","Client Unknown or Unauthorized");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Forbidden
                };
            }

            request.RequestServer = new()
            {
                Guid = client.Guid,
                Name = client.Name
            };

            var telemetry = new StreamFlowTelemetryBO
            {
                ClientGuid = client.Guid,
                RequestGuid = request.MessageQueue.RequestGuid,
                ConsumerGuid = request.MessageQueue.ConsumerGuid
            };
            
            var c = _cachingService.Clients.FirstOrDefault(x => x.Guid == request.MessageQueue.Recipient);

            if (c != null)
            {
                if (_cachingService.PendingMethodCalls.TryRemove(request.MessageQueue.RequestGuid, out TaskCompletionSource<StreamFlowMessageBO> methodCallCompletionSource))
                {
                    methodCallCompletionSource.SetResult(request.MessageQueue);
                }

                Console.WriteLine("Response for Invoked Method Received");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted
                };
            }
            
            if (_cachingService.AbsoluteClients.All(x => x.Guid != request.MessageQueue.Recipient))
            {
                Console.WriteLine($"Connection with ID {request.RequestServer.Guid} : {request.RequestServer.Name} has invalid recipient");
                goto returnYield;
            }

            if (!_streamFlowConfiguration.QueueMessages)
            {
                Console.WriteLine($"[Message Queue Disabled]; Message from connection with ID {request.RequestServer.Guid} : {request.RequestServer.Name} has been dropped; Recipient unavailable");
                goto returnYield;
            }

            if (_cachingService.QueuedMessages.Where(i => i.Recipient == request.MessageQueue.Recipient).Count() > _streamFlowConfiguration.QueueDepth)
            {
                Console.WriteLine($"Message from connection with ID {request.RequestServer.Guid} : {request.RequestServer.Name} cannot be queued: Queue depth has been exhausted");
                goto returnYield;
            }
                    
            _cachingService.QueuedMessages.Add(request.MessageQueue);
            Console.WriteLine($"Message from connection with ID {request.RequestServer.Guid} : {request.RequestServer.Name} has been queued; Recipient unavailable");

            returnYield:

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };

        }
    }
}
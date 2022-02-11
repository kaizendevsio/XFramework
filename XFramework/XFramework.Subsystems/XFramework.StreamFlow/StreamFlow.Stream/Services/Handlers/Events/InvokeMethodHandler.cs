using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
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
using XFramework.Integration.Interfaces;

namespace StreamFlow.Stream.Services.Handlers.Events
{
    public class InvokeMethodHandler : CommandBaseHandler, IRequestHandler<InvokeMethodQuery, QueryResponseBO<StreamFlowInvokeResponse>>
    {
        public InvokeMethodHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext, StreamFlowConfiguration streamFlowConfiguration, IHelperService helperService)
        {
            _helperService = helperService;
            _cachingService = cachingService;
            _hubContext = hubContext;
            _streamFlowConfiguration = streamFlowConfiguration;
        }
        public async Task<QueryResponseBO<StreamFlowInvokeResponse>> Handle(InvokeMethodQuery request, CancellationToken cancellationToken)
        {
            // Check if Client is Registered
            var client = _cachingService.Clients.FirstOrDefault(x => x.StreamId == request.Context.ConnectionId);
            if (client == null)
            {
                Console.WriteLine($"Unknown or unauthorized client detected");
                await _hubContext.Clients.Client(request.Context.ConnectionId).SendAsync("TelemetryCall","Client Unknown or Unauthorized");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Forbidden
                };
            }

            request.RequestServer = new()
            {
                RequestId = client.Guid,
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
                _helperService.StopWatch.Start("Invoked Method");
                var methodCallCompletionSource = new TaskCompletionSource<StreamFlowMessageBO>();
                //methodCallCompletionSource.RunContinuationsAsynchronously();
                if (!_cachingService.PendingMethodCalls.TryAdd(request.MessageQueue.RequestGuid,methodCallCompletionSource))
                {
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.InternalServerError,
                        Message = $"Error while invoking method '{request.MessageQueue.CommandName}' on {request.MessageQueue.Recipient}"
                    };
                }
                
                await _hubContext.Clients.Client(c.StreamId).SendAsync(request.MessageQueue.CommandName, request.MessageQueue.Data, request.MessageQueue.Message, telemetry, cancellationToken);
                var response = await methodCallCompletionSource.Task.ConfigureAwait(false);

                _helperService.StopWatch.Stop("Invoke Response Received");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    Response = new ()
                    {
                        HttpStatusCode = response.Adapt<CmdResponseBO>().HttpStatusCode,
                        Response = response.Data
                    }
                };

            }

            if (_cachingService.AbsoluteClients.All(x => x.Guid != request.MessageQueue.Recipient))
            {
                Console.WriteLine($"Connection with ID {request.RequestServer.RequestId} : {request.RequestServer.Name} has invalid recipient");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Response = new()
                    {
                        HttpStatusCode = HttpStatusCode.NotFound
                    }
                };
            }


            Console.WriteLine($"Message from connection with ID {request.RequestServer.RequestId} : {request.RequestServer.Name} has been dropped; Recipient unavailable");
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
}
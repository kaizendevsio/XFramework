using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.DataAccess.Commands.Handlers;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Enums;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Enums;
using StreamFlow.Stream.Hubs.V1;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events
{
    public class PushMessageHandler : CommandBaseHandler, IRequestHandler<PushMessageCmd, CmdResponseBO<PushMessageCmd>>
    {
        public PushMessageHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext, StreamFlowConfiguration streamFlowConfiguration)
        {
            _streamFlowConfiguration = streamFlowConfiguration;
            _hubContext = hubContext;
            _cachingService = cachingService;
        }
        
        public async Task<CmdResponseBO<PushMessageCmd>> Handle(PushMessageCmd request, CancellationToken cancellationToken)
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
                Guid = client.Guid,
                Name = client.Name
            };

            var telemetry = new StreamFlowTelemetryBO
            {
                ClientGuid = client.Guid,
                RequestGuid = request.MessageQueue.RequestGuid,
                ConsumerGuid = request.MessageQueue.ConsumerGuid
            };

            // Execute Sending Message
            switch (request.MessageQueue.ExchangeType)
            {
                case MessageExchangeType.FanOut:
                    await _hubContext.Clients.All.SendAsync(request.MessageQueue.CommandName, request.MessageQueue.Data, request.MessageQueue.Message, JsonSerializer.Serialize(telemetry), cancellationToken: cancellationToken);
                    break;
                case MessageExchangeType.Direct:
                    var c = _cachingService.Clients.FirstOrDefault(x => x.Guid == request.MessageQueue.Recipient);

                    if (c != null)
                    {
                        await _hubContext.Clients.Client(c.StreamId).SendAsync(request.MessageQueue.CommandName, request.MessageQueue.Data, request.MessageQueue.Message, JsonSerializer.Serialize(telemetry), cancellationToken);
                        break;
                    }

                    if (_cachingService.AbsoluteClients.All(x => x.Guid != request.MessageQueue.Recipient))
                    {
                        Console.WriteLine($"Connection with ID {request.RequestServer.Guid} : {request.RequestServer.Name} has invalid recipient");
                        return new()
                        {
                            HttpStatusCode = HttpStatusCode.NotFound
                        };
                    }

                    if (!_streamFlowConfiguration.QueueMessages)
                    {
                        Console.WriteLine($"[Message Queue Disabled]; Message from connection with ID {request.RequestServer.Guid} : {request.RequestServer.Name} has been dropped; Recipient unavailable");
                        break;
                    }

                    if (_cachingService.QueuedMessages.Where(i => i.Recipient == request.MessageQueue.Recipient).Count() > _streamFlowConfiguration.QueueDepth)
                    {
                        Console.WriteLine($"Message from connection with ID {request.RequestServer.Guid} : {request.RequestServer.Name} cannot be queued: Queue depth has been exhausted");
                        break;
                    }
                    
                    _cachingService.QueuedMessages.Add(request.MessageQueue);
                    Console.WriteLine($"Message from connection with ID {request.RequestServer.Guid} : {request.RequestServer.Name} has been queued; Recipient unavailable");
                   
                    break;
                case MessageExchangeType.Topic:
                    await _hubContext.Clients.Group(request.MessageQueue.Recipient.ToString()).SendAsync(request.MessageQueue.CommandName, request.MessageQueue.Data, request.MessageQueue.Message, JsonSerializer.Serialize(telemetry), cancellationToken: cancellationToken);
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
}
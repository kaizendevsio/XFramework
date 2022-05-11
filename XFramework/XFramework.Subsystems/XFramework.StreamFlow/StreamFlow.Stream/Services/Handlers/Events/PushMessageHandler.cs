using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BinaryPack;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.DataAccess.Commands.Handlers;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Enums;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Generic.Enums;
using StreamFlow.Stream.Hubs.V1;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events
{
    public class PushMessageHandler : CommandBaseHandler, IRequestHandler<PushMessageCmd, CmdResponse<PushMessageCmd>>
    {
        public PushMessageHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext, StreamFlowConfiguration streamFlowConfiguration)
        {
            _streamFlowConfiguration = streamFlowConfiguration;
            _hubContext = hubContext;
            _cachingService = cachingService;
        }
        
        public async Task<CmdResponse<PushMessageCmd>> Handle(PushMessageCmd request, CancellationToken cancellationToken)
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

            var contract = new StreamFlowContract()
            {
                Data = request.MessageQueue.Data,
                Message = request.MessageQueue.Message,
                Telemetry = telemetry
            };
            
            // Execute Sending Message
            switch (request.MessageQueue.ExchangeType)
            {
                case MessageExchangeType.FanOut:
                    await _hubContext.Clients.All.SendAsync(request.MessageQueue.CommandName, contract, cancellationToken: cancellationToken);
                    break;
                case MessageExchangeType.Direct:
                    var currentClient = new StreamFlowClientBO();

                    var availableClients = _cachingService.Clients.Where(x => x.Guid == request.MessageQueue.Recipient).ToList();
                    var count = availableClients.Count;
                    
                    if (count > 1)
                    {
                        var cachedClient = _cachingService.LatestClients.FirstOrDefault(x => x.Guid == request.MessageQueue.Recipient);
                        if (cachedClient is null)
                        {
                            currentClient = availableClients.First();
                            _cachingService.LatestClients.Add(currentClient);
                        }
                        else
                        {
                            var cachedClientIndex = _cachingService.Clients.IndexOf(cachedClient);
                            currentClient = count >= (cachedClientIndex + 1) 
                                ? availableClients.First() 
                                : availableClients.ElementAt(cachedClientIndex + 1);
                            
                            _cachingService.LatestClients.Remove(cachedClient);
                            _cachingService.LatestClients.Add(currentClient);
                        }
                    }
                    else
                    {
                        currentClient = availableClients.FirstOrDefault();
                    }

                    if (currentClient != null)
                    {
                        Console.WriteLine($"Action: {request.MessageQueue.ExchangeType} | Request ID: {telemetry.RequestGuid} | {request.RequestServer.Name} -> {currentClient.Name}");
                        var ba = BinaryConverter.Serialize(contract);
                        await _hubContext.Clients.Client(currentClient.StreamId).SendAsync(request.MessageQueue.CommandName, ba, cancellationToken);
                        break;
                    }

                    if (_cachingService.AbsoluteClients.All(x => x.Guid != request.MessageQueue.Recipient))
                    {
                        Console.WriteLine($"Connection with ID {request.RequestServer.RequestId} : {request.RequestServer.Name} has invalid recipient");
                        return new()
                        {
                            HttpStatusCode = HttpStatusCode.NotFound
                        };
                    }

                    if (!_streamFlowConfiguration.QueueMessages)
                    {
                        Console.WriteLine($"[Message Queue Disabled]; Message from connection with ID {request.RequestServer.RequestId} : {request.RequestServer.Name} has been dropped; Recipient unavailable");
                        break;
                    }

                    if (_cachingService.QueuedMessages.Where(i => i.Recipient == request.MessageQueue.Recipient).Count() > _streamFlowConfiguration.QueueDepth)
                    {
                        Console.WriteLine($"Message from connection with ID {request.RequestServer.RequestId} : {request.RequestServer.Name} cannot be queued: Queue depth has been exhausted");
                        break;
                    }
                    
                    _cachingService.QueuedMessages.Add(request.MessageQueue);
                    Console.WriteLine($"Message from connection with ID {request.RequestServer.RequestId} : {request.RequestServer.Name} has been queued; Recipient unavailable");
                   
                    break;
                case MessageExchangeType.Topic:
                    await _hubContext.Clients.Group(request.MessageQueue.Recipient.ToString()).SendAsync(request.MessageQueue.CommandName, contract, cancellationToken: cancellationToken);
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
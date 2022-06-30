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
            var client = _cachingService.Clients.FirstOrDefault(x => x.Value.StreamId == request.Context.ConnectionId);
            if (client.Value == null)
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
                RequestId = client.Value.Guid,
                Name = client.Value.Name
            };

            var telemetry = new StreamFlowTelemetryBO
            {
                ClientGuid = client.Value.Guid,
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

                    var availableClients = _cachingService.Clients.Where(x => x.Value.Guid == request.MessageQueue.Recipient).Select(i => i.Value).ToList();
                    var count = availableClients.Count;
                    
                    if (count > 1)
                    {
                        var cachedClient = _cachingService.LatestClients.Select(i => i.Value).FirstOrDefault(x => x.Guid == request.MessageQueue.Recipient);
                        if (cachedClient is null)
                        {
                            var cc = availableClients.First();
                            currentClient = cc;
                            
                            ReTryAddLatestClients:
                            if (!_cachingService.LatestClients.TryAdd(_cachingService.LatestClients.Count, currentClient))
                            {
                                goto ReTryAddLatestClients;
                            }
                        }
                        else
                        {
                            var cachedClientIndex = availableClients.IndexOf(cachedClient);
                            currentClient = (cachedClientIndex + 1) >= count 
                                ? availableClients.First()
                                : availableClients.ElementAt(cachedClientIndex + 1);
                            
                            ReTryRemoveLatestClients:
                            var tmpIndex = _cachingService.LatestClients.FirstOrDefault(i => i.Value.Guid == cachedClient.Guid);
                            if (!_cachingService.LatestClients.TryRemove(tmpIndex.Key, out _))
                            {
                                goto ReTryRemoveLatestClients;
                            }
                                
                            ReTryAddLatestClients:
                            if (!_cachingService.LatestClients.TryAdd(0, currentClient))
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
                        Console.WriteLine($"Action: {request.MessageQueue.ExchangeType} | Request ID: {telemetry.RequestGuid} | {request.RequestServer.Name} -> {currentClient.Name}");
                        await _hubContext.Clients.Client(currentClient.StreamId).SendAsync(request.MessageQueue.CommandName, contract, cancellationToken);
                        break;
                    }

                    if (_cachingService.AbsoluteClients.All(x => x.Value.Guid != request.MessageQueue.Recipient))
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

                    if (_cachingService.QueuedMessages.Where(i => i.Value.Recipient == request.MessageQueue.Recipient).Count() > _streamFlowConfiguration.QueueDepth)
                    {
                        Console.WriteLine($"Message from connection with ID {request.RequestServer.RequestId} : {request.RequestServer.Name} cannot be queued: Queue depth has been exhausted");
                        break;
                    }
                    
                    _cachingService.QueuedMessages.TryAdd(Guid.NewGuid(), request.MessageQueue);
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
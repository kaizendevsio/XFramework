using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Stream.Hubs.V1;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events
{
    public class RegisterClientHandler : CommandBaseHandler, IRequestHandler<RegisterClientCmd, CmdResponse<RegisterClientCmd>>
    {
        public RegisterClientHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext, StreamFlowConfiguration streamFlowConfiguration)
        {
            _cachingService = cachingService;
            _hubContext = hubContext;
            _streamFlowConfiguration = streamFlowConfiguration;
        }
        
        public async Task<CmdResponse<RegisterClientCmd>> Handle(RegisterClientCmd request, CancellationToken cancellationToken)
        {
            Again:
            var y = _cachingService.Clients.TryAdd(_cachingService.Clients.Count, new()
            {
                StreamId = request.Context.ConnectionId,
                Guid = request.Client.Guid,
                Name = request.Client.Name
            });
            if (!y)
            {
                goto Again;
            }
            RememberClient(request);

            Console.WriteLine($"Connection Registered with ID {request.Context.ConnectionId} : {request.Client.Guid} [{request.Context.Features.Get<IHttpTransportFeature>()?.TransportType.ToString()}] : {request.Client.Name}");
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };

        }
        private async Task RememberClient(RegisterClientCmd request)
        {
            if (_cachingService.AbsoluteClients.All(i => i.Value.Guid != request.Client.Guid))
            {
                _cachingService.AbsoluteClients.TryAdd(_cachingService.AbsoluteClients.Count , new()
                {
                    StreamId = request.Context.ConnectionId,
                    Guid = request.Client.Guid,
                    Name = request.Client.Name
                });
            }
            else
            {
                var client = _cachingService.AbsoluteClients.FirstOrDefault(i => i.Value.Guid != request.Client.Guid);
                client.Value.StreamId = request.Context.ConnectionId;
            }
        }
    }
}
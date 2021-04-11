using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace StreamFlow.Stream.Hubs.V1
{
    public class MessageQueueHub : HubBase
    {
        public MessageQueueHub(IMediator mediator, ICachingService cachingService)
        {
            _cachingService = cachingService;
            _mediator = mediator;
        }
        public override async Task OnConnectedAsync()
        {
            Debug.WriteLine(Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        
        public async Task<HttpStatusCode> Push(StreamFlowMessageBO request)
        {
            var client = _cachingService.Clients.FirstOrDefault(x => x.StreamId == Context.ConnectionId);
            if (client == null)
            {
                Debug.WriteLine($"Unknown or unauthorized client detected");
                Clients.Caller.SendAsync("TelemetryCall","Client Unknown or Unauthorized");
                return HttpStatusCode.Forbidden;
            }
            
            Thread.Sleep(1000);
            PushMessageCmd entity = new()
            {
                MessageQueue = request,
                RequestServer = new()
                {
                    ClientGuid = client.Guid
                }
            };
            await _mediator.Send(entity).ConfigureAwait(false);
            return HttpStatusCode.Accepted;
        }
        
        public async Task<HttpStatusCode> Subscribe(StreamFlowClientBO request)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, request.Queue.Guid.ToString());
            return HttpStatusCode.Accepted;
        }

        public async Task<HttpStatusCode> Register(StreamFlowClientBO request)
        {
            _cachingService.Clients.Add(new()
            {
                StreamId = Context.ConnectionId,
                Guid = request.Guid
            });
            return HttpStatusCode.Accepted;
        }
        public async Task<HttpStatusCode> Unsubscribe(StreamFlowClientBO request)
        {
            await Groups.RemoveFromGroupAsync(request.StreamId, request.Queue.Guid.ToString());
            return HttpStatusCode.Accepted;
        }
    }
}
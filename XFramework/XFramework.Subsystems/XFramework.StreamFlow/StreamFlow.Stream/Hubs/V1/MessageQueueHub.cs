using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Enums;

namespace StreamFlow.Stream.Hubs.V1
{
    public class MessageQueueHub : HubBase
    {
        public MessageQueueHub(IMediator mediator)
        {
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

        
        public async Task Push(StreamFlowMessageBO request)
        {
            await _mediator.Send(request).ConfigureAwait(false);
        }
        
        public async Task Subscribe(StreamFlowClientBO request)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, request.Queue.Guid.ToString());
        }

        public async Task Unsubscribe(StreamFlowClientBO request)
        {
            await Groups.RemoveFromGroupAsync(request.StreamId, request.Queue.Guid.ToString());
        }
    }
}
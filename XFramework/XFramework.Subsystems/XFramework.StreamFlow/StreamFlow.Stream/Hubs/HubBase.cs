using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace StreamFlow.Stream.Hubs
{
    public class HubBase : Hub
    {
        public IMediator _mediator;
    }
}
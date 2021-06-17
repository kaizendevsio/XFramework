using MediatR;
using Microsoft.AspNetCore.SignalR;
using RBS.Core.Interfaces;

namespace RBS.Api.Hubs
{
    public class HubBase : Hub
    {
        public IMediator _mediator;
        public ICachingService _cachingService;
    }
}
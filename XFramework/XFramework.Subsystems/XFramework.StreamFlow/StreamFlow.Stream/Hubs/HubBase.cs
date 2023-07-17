using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;

namespace StreamFlow.Stream.Hubs;

public class HubBase : Hub
{
    public IMediator _mediator;
    public ICachingService _cachingService;
}
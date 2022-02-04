using MediatR;
using Microsoft.AspNetCore.SignalR;
using XFramework.Core.Interfaces;

namespace XFramework.Api.Hubs
{
    public class HubBase : Hub
    {
        public IMediator Mediator;
        public ICachingService CachingService;
        public IConfiguration Configuration;
    }
}
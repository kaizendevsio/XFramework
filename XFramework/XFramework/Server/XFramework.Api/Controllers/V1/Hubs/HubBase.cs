using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using XFramework.Core.Interfaces;

namespace XFramework.Api.Controllers.V1.Hubs
{
    public class HubBase : Hub
    {
        public IMediator _mediator;
        public ICachingService _cachingService;
        public IConfiguration _configuration;
    }
}
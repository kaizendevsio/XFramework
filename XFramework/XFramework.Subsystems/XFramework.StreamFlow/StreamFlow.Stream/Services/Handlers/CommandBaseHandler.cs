using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Stream.Hubs.V1;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers
{
    public class CommandBaseHandler
    {
        public IMediator _mediator;
        public IDataLayer _dataLayer;
        public ICachingService _cachingService;
        public IHubContext<MessageQueueHub> _hubContext;
        public StreamFlowConfiguration _streamFlowConfiguration;

    }
}

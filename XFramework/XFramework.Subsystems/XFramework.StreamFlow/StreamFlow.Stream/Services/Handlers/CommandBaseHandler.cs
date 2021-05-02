using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Stream.Hubs.V1;
using XFramework.Domain.Generic.Configurations;
using XFramework.Integration.Interfaces;

namespace StreamFlow.Stream.Services.Handlers
{
    public class CommandBaseHandler
    {
        public IMediator _mediator;
        public IDataLayer _dataLayer;
        public IHelperService _helperService;
        public ICachingService _cachingService;
        public IHubContext<MessageQueueHub> _hubContext;
        public StreamFlowConfiguration _streamFlowConfiguration;

    }
}

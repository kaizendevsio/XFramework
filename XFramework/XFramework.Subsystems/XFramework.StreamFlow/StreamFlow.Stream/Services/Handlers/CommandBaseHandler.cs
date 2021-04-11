using System;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Stream.Hubs.V1;

namespace StreamFlow.Core.DataAccess.Commands.Handlers
{
    public class CommandBaseHandler
    {
        public IDataLayer _dataLayer;
        public ICachingService _cachingService;
        public IHubContext<MessageQueueHub> _hubContext;

    }
}

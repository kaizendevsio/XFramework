using System;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;

namespace StreamFlow.Core.DataAccess.Commands.Handlers
{
    public class CommandBaseHandler
    {
        public IDataLayer _dataLayer;
        public ICachingService _cachingService;

    }
}

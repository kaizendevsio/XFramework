using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using StreamFlow.Core.DataAccess.Commands.Entity.Events;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.Enums;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Enums;

namespace StreamFlow.Core.DataAccess.Commands.Handlers.Events
{
    public class PushMessageHandler : CommandBaseHandler, IRequestHandler<PushMessageCmd, CmdResponseBO<PushMessageCmd>>
    {
        public PushMessageHandler(ICachingService cachingService)
        {
            _cachingService = cachingService;
        }
        
        public async Task<CmdResponseBO<PushMessageCmd>> Handle(PushMessageCmd request, CancellationToken cancellationToken)
        {
            switch (request.MessageQueue.ExchangeType)
            {
                case MessageExchangeType.FanOut:
                    await Clients.All.SendAsync(request.MessageQueue.MethodName, request.MessageQueue.Message, cancellationToken: cancellationToken);
                    break;
                case MessageExchangeType.Direct:
                    await Clients.Client(_cachingService.Clients.Single(x => x.Guid == request.MessageQueue.Recipient).StreamId).SendAsync(request.MessageQueue.MethodName, request.MessageQueue.Message);
                    break;
                case MessageExchangeType.Topic:
                    await Clients.Group(request.MessageQueue.Recipient.ToString()).SendAsync(request.MessageQueue.MethodName, request.MessageQueue.Message, cancellationToken: cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            return new();
        }
    }
}
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.DataAccess.Commands.Handlers;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Enums;
using StreamFlow.Stream.Hubs.V1;
using StreamFlow.Stream.Services.Entity.Events;

namespace StreamFlow.Stream.Services.Handlers.Events
{
    public class PushMessageHandler : CommandBaseHandler, IRequestHandler<PushMessageCmd, CmdResponseBO<PushMessageCmd>>
    {
        public PushMessageHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext)
        {
            _hubContext = hubContext;
            _cachingService = cachingService;
        }
        
        public async Task<CmdResponseBO<PushMessageCmd>> Handle(PushMessageCmd request, CancellationToken cancellationToken)
        {
            switch (request.MessageQueue.ExchangeType)
            {
                case MessageExchangeType.FanOut:
                    await _hubContext.Clients.All.SendAsync(request.MessageQueue.MethodName, request.MessageQueue.Message, cancellationToken: cancellationToken);
                    break;
                case MessageExchangeType.Direct:
                    await _hubContext.Clients.Client(_cachingService.Clients.Single(x => x.Guid == request.MessageQueue.Recipient).StreamId).SendAsync(request.MessageQueue.MethodName, request.MessageQueue.Message);
                    break;
                case MessageExchangeType.Topic:
                    await _hubContext.Clients.Group(request.MessageQueue.Recipient.ToString()).SendAsync(request.MessageQueue.MethodName, request.MessageQueue.Message, cancellationToken: cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            return new();
        }
    }
}
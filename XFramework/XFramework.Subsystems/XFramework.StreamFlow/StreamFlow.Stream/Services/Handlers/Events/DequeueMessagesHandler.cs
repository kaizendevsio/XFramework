using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Stream.Hubs.V1;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events
{
    public class DequeueMessagesHandler : CommandBaseHandler,
        IRequestHandler<DequeueMessagesCmd, CmdResponse<DequeueMessagesCmd>>
    {
        public DequeueMessagesHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext,
            StreamFlowConfiguration streamFlowConfiguration, IMediator mediator)
        {
            _mediator = mediator;
            _cachingService = cachingService;
            _hubContext = hubContext;
            _streamFlowConfiguration = streamFlowConfiguration;
        }

        public async Task<CmdResponse<DequeueMessagesCmd>> Handle(DequeueMessagesCmd request, CancellationToken cancellationToken)
        {
            var queuedMessages = _cachingService.QueuedMessages.Where(i => i.Value.Recipient == request.Client.Guid).ToList();
            if (queuedMessages.Any())
            {
                Console.WriteLine($"Dequeuing items from cache..");
                foreach (var message in queuedMessages)
                {
                    var entity = new PushMessageCmd()
                    {
                        Context = request.Context,
                        MessageQueue = message.Value
                    };
                    await _mediator.Send(entity).ConfigureAwait(false);
                    _cachingService.QueuedMessages.TryRemove(message);
                }
                Console.WriteLine($"Dequeued {queuedMessages.Count} item(s) from cache");
            }
            

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Stream.Hubs.V1;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events
{
    public class DequeueMessagesHandler : CommandBaseHandler,
        IRequestHandler<DequeueMessagesCmd, CmdResponseBO<DequeueMessagesCmd>>
    {
        public DequeueMessagesHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext,
            StreamFlowConfiguration streamFlowConfiguration, IMediator mediator)
        {
            _mediator = mediator;
            _cachingService = cachingService;
            _hubContext = hubContext;
            _streamFlowConfiguration = streamFlowConfiguration;
        }

        public async Task<CmdResponseBO<DequeueMessagesCmd>> Handle(DequeueMessagesCmd request, CancellationToken cancellationToken)
        {
            var queuedMessages = _cachingService.QueuedMessages.Where(i => i.Recipient == request.Client.Guid);
            foreach (var message in queuedMessages)
            {
                var entity = new PushMessageCmd()
                {
                    Context = request.Context,
                    MessageQueue = message
                };
                await _mediator.Send(entity).ConfigureAwait(false);
            }

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Stream.Hubs;
using StreamFlow.Stream.Services.Entity.Events;
using XFramework.Domain.Generic.Configurations;

namespace StreamFlow.Stream.Services.Handlers.Events;

public class DequeueMessagesHandler : IRequestHandler<DequeueMessagesCmd, CmdResponse<DequeueMessagesCmd>>
{
    private readonly ICachingService _cachingService;
    private readonly IHubContext<MessageQueueHub> _hubContext;
    private readonly StreamFlowConfiguration _streamFlowConfiguration;
    private readonly IMediator _mediator;

    public DequeueMessagesHandler(ICachingService cachingService, IHubContext<MessageQueueHub> hubContext, StreamFlowConfiguration streamFlowConfiguration, IMediator mediator)
    {
        _mediator = mediator;
        _cachingService = cachingService;
        _hubContext = hubContext;
        _streamFlowConfiguration = streamFlowConfiguration;
        _mediator = mediator;
        _cachingService = cachingService;
        _hubContext = hubContext;
        _streamFlowConfiguration = streamFlowConfiguration;
    }

    public async Task<CmdResponse<DequeueMessagesCmd>> Handle(DequeueMessagesCmd request, CancellationToken cancellationToken)
    {
        var queuedMessages = _cachingService.QueuedMessages.Where(i => i.Value.RecipientId == request.Client.Guid).ToList();
        if (queuedMessages.Any())
        {
            Console.WriteLine($"Dequeuing items from cache..");
            foreach (var message in queuedMessages)
            {
                var entity = new PushMessageCmd()
                {
                    Context = request.Context,
                    Message = message.Value
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
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Generic.Abstractions;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Stream.Services.Entity.Events;

namespace StreamFlow.Stream.Hubs;

public class MessageQueueHub : Hub<IStreamFlow>
{
    private IMediator _mediator;
    private ICachingService _cachingService;
    private readonly ILogger<MessageQueueHub> _logger;

    public MessageQueueHub(IMediator mediator, ICachingService cachingService, ILogger<MessageQueueHub> logger) 
    {
        _cachingService = cachingService;
        _logger = logger;
        _mediator = mediator;
    }
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("New Connection Detected with ID {ContextConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var client = _cachingService.Clients.FirstOrDefault(i => i.Value.StreamId == Context.ConnectionId);
        var cachedClient = _cachingService.LatestClients.FirstOrDefault(x => x.Value.StreamId == Context.ConnectionId);
        _cachingService.Clients.Remove(client.Key, out var a);
        _cachingService.LatestClients.Remove(cachedClient.Key, out _);

        await base.OnDisconnectedAsync(exception);
        _logger.LogInformation("Connection Lost and Unregistered with ID {ContextConnectionId} : {ValueGuid} : {ValueName}", Context.ConnectionId, a?.Id, a?.Name);
    }

    public StreamFlowInvokeResponse Invoke(StreamFlowMessage request)
    {
        var entity = new InvokeMethodQuery()
        {
            Context = Context,
            MessageQueue = request
        };
        var response = _mediator.Send(entity).Result;
        return response.Response;
    }
    public async Task<HttpStatusCode> InvokeResponse(StreamFlowMessage request)
    {
        var entity = new InvokeMethodResponseCmd()
        {
            Context = Context,
            MessageQueue = request
        };
        var response = await _mediator.Send(entity).ConfigureAwait(false);
        return response.HttpStatusCode;
    }
    public async Task<HttpStatusCode> Push(StreamFlowMessage request)
    {
        var entity = new PushMessageCmd()
        {
            Context = Context,
            Message = request
        };
        var response = await _mediator.Send(entity).ConfigureAwait(false);
        return response.HttpStatusCode;
    }
    public async Task<HttpStatusCode> Subscribe(StreamFlowClient request)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, request.Queue.Name);
        return HttpStatusCode.Accepted;
    }
    public async Task<HttpStatusCode> Register(StreamFlowClient request)
    {
        var entity = new RegisterClientCmd()
        {
            Context = Context,
            Client = request
        };
        var response = await _mediator.Send(entity).ConfigureAwait(false);
        await _mediator.Send(new DequeueMessagesCmd(){Client = request, Context = Context}).ConfigureAwait(false);
            
        return response.HttpStatusCode;
    }
    public async Task<HttpStatusCode> Unsubscribe(StreamFlowClient request)
    {
        await Groups.RemoveFromGroupAsync(request.StreamId, request.Queue.Id.ToString());
        return HttpStatusCode.Accepted;
    }
}
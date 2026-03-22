using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Domain.Shared.Abstractions;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Stream.Interfaces;
using StreamFlow.Stream.Services;

namespace StreamFlow.Stream.Hubs;

public class MessageQueueHub : Hub<IStreamFlow>
{
    private readonly IStreamFlowService _streamFlowService;
    private readonly ICachingService _cachingService;
    private readonly IQueryExecutionService _queryExecutionService;
    private readonly ILogger<MessageQueueHub> _logger;

    public MessageQueueHub(
        IStreamFlowService streamFlowService,
        ICachingService cachingService,
        IQueryExecutionService queryExecutionService,
        ILogger<MessageQueueHub> logger)
    {
        _streamFlowService = streamFlowService ?? throw new ArgumentNullException(nameof(streamFlowService));
        _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
        _queryExecutionService = queryExecutionService ?? throw new ArgumentNullException(nameof(queryExecutionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("New Connection Detected with ID {ContextConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        StreamFlowClient removedClient = null;

        if (_cachingService.ClientKeyByStreamId.TryRemove(Context.ConnectionId, out var clientKey))
        {
            _cachingService.Clients.TryRemove(clientKey, out removedClient);

            // Clean up ClientsByServiceId reverse index
            if (removedClient != null &&
                _cachingService.ClientsByServiceId.TryGetValue(removedClient.Id, out var bag))
            {
                // ConcurrentBag does not support removal; rebuild without the removed key
                var updated = new ConcurrentBag<int>(bag.Where(k => k != clientKey));
                if (updated.IsEmpty)
                {
                    _cachingService.ClientsByServiceId.TryRemove(removedClient.Id, out _);
                }
                else
                {
                    _cachingService.ClientsByServiceId[removedClient.Id] = updated;
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
        _logger.LogInformation("Connection Lost and Unregistered with ID {ContextConnectionId} : {ValueGuid} : {ValueName}", Context.ConnectionId, removedClient?.Id, removedClient?.Name);
    }

    public async Task<StreamFlowInvokeResponse> Invoke(StreamFlowMessage request)
    {
        var result = await _streamFlowService.InvokeMethodAsync(request, Context, CancellationToken.None);

        if (result.IsSuccess && result.Data != null)
        {
            return result.Data;
        }

        // Return error response
        return new StreamFlowInvokeResponse
        {
            HttpStatusCode = (HttpStatusCode)result.StatusCode,
            Message = result.Message ?? "Method invocation failed"
        };
    }
    public async Task<HttpStatusCode> InvokeResponse(StreamFlowMessage request)
    {
        var result = await _streamFlowService.InvokeResponseAsync(request, Context);
        return (HttpStatusCode)result.StatusCode;
    }
    public async Task<HttpStatusCode> Push(StreamFlowMessage request)
    {
        var result = await _streamFlowService.PushMessageAsync(request, Context);
        return (HttpStatusCode)result.StatusCode;
    }
    public async Task<HttpStatusCode> Subscribe(StreamFlowClient request)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, request.Queue.Name);
        return HttpStatusCode.Accepted;
    }
    public async Task<HttpStatusCode> Register(StreamFlowClient request)
    {
        var result = await _streamFlowService.RegisterClientAsync(request, Context);
        
        // Trigger dequeue for newly registered client
        await _streamFlowService.DequeueMessagesAsync(request, Context);
            
        return (HttpStatusCode)result.StatusCode;
    }
    public async Task<HttpStatusCode> Unsubscribe(StreamFlowClient request)
    {
        await Groups.RemoveFromGroupAsync(request.StreamId, request.Queue.Id.ToString());
        return HttpStatusCode.Accepted;
    }

    public async Task<byte[]> ExecuteQuery(byte[] queryDescriptorBytes)
    {
        return await _queryExecutionService.ExecuteAsync(queryDescriptorBytes, Context.ConnectionAborted);
    }

    public async IAsyncEnumerable<byte[]> StreamQuery(
        byte[] queryDescriptorBytes,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in _queryExecutionService.ExecuteStreamAsync(queryDescriptorBytes, ct))
        {
            yield return chunk;
        }
    }

    public async Task<byte[]> ExecuteChanges(byte[] saveChangesRequestBytes)
    {
        return await _queryExecutionService.ExecuteChangesAsync(saveChangesRequestBytes, Context.ConnectionAborted);
    }
}
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using StreamFlow.Core.Interfaces;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Domain.Shared.Contracts.Requests;
using StreamFlow.Domain.Shared.Contracts.Responses;
using StreamFlow.Domain.Shared.Enums;
using StreamFlow.Stream.Hubs;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Configurations;
using StreamFlow.Core.Services;

namespace StreamFlow.Stream.Services;

/// <summary>
/// Service for StreamFlow SignalR messaging operations.
/// Consolidates handler logic from MediatR handlers into direct service methods.
/// Preserves all Channel-based queueing and performance optimizations.
/// </summary>
public class StreamFlowService : IStreamFlowService
{
    private readonly ICachingService _cachingService;
    private readonly IHubContext<MessageQueueHub> _hubContext;
    private readonly StreamFlowConfiguration _configuration;
    private readonly ILogger<StreamFlowService> _logger;
    private static long _clientKeyCounter = 100000000;

    public StreamFlowService(
        ICachingService cachingService,
        IHubContext<MessageQueueHub> hubContext,
        StreamFlowConfiguration configuration,
        ILogger<StreamFlowService> logger)
    {
        _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result> PushMessageAsync(
        StreamFlowMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        // Check if Client is Registered
        var client = _cachingService.Clients.FirstOrDefault(x => x.Value.StreamId == context.ConnectionId);
        if (client.Value == null)
        {
            _logger.LogWarning("Unknown or unauthorized client detected. ConnectionId: {ConnectionId}",
                context.ConnectionId);
            
            await _hubContext.Clients.Client(context.ConnectionId)
                .SendAsync("TelemetryCall", "Client Unknown or Unauthorized", cancellationToken);
            
            return Result.Failure("Client Unknown or Unauthorized", 403);
        }

        // Execute Sending Message
        try
        {
            switch (message.ExchangeType)
            {
                case MessageExchangeType.FanOut:
                    await _hubContext.Clients.All.SendAsync(message.CommandName, message,
                        cancellationToken: cancellationToken);
                    _logger.LogInformation("FanOut message sent. RequestId: {RequestId}, Sender: {SenderName}",
                        message.RequestId, client.Value.Name);
                    break;

                case MessageExchangeType.Direct:
                    await HandleDirectMessageAsync(message, client.Value, cancellationToken);
                    break;

                case MessageExchangeType.Topic:
                    await _hubContext.Clients.Group(message.Topic)
                        .SendAsync(message.CommandName, message, cancellationToken: cancellationToken);
                    _logger.LogInformation("Topic message sent. RequestId: {RequestId}, Topic: {Topic}, Sender: {SenderName}",
                        message.RequestId, message.Topic, client.Value.Name);
                    break;

                default:
                    return Result.Failure(
                        $"Unsupported exchange type: {message.ExchangeType}", 400);
            }

            return Result.Success("Message pushed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing message. RequestId: {RequestId}", message.RequestId);
            return Result.Failure("An error occurred processing the message", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> RegisterClientAsync(
        StreamFlowClient client,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        var clientKey = GenerateUniqueClientKey();
        var clientInfo = new StreamFlowClient
        {
            StreamId = context.ConnectionId,
            Id = client.Id,
            Name = client.Name,
            Queue = client.Queue,
            ConnectedAt = DateTime.UtcNow
        };

        // Add client with retry logic
        const int maxAttempts = 100;
        int attempts = 0;
        bool added = false;

        while (attempts < maxAttempts && !added)
        {
            added = _cachingService.Clients.TryAdd((int)clientKey, clientInfo);
            if (!added)
            {
                clientKey = GenerateUniqueClientKey();
                attempts++;
            }
        }

        if (!added)
        {
            _logger.LogError(
                "Failed to register client after {MaxAttempts} attempts. ConnectionId: {ConnectionId}, ClientId: {ClientId}",
                maxAttempts, context.ConnectionId, client.Id);
            return Result.Failure("Failed to register client", 500);
        }

        RememberClient(client, context);

        var transportType = context.Features.Get<IHttpTransportFeature>()?.TransportType.ToString() ?? "Unknown";
        _logger.LogInformation(
            "Client registered. ConnectionId: {ConnectionId}, ClientId: {ClientId}, Transport: {TransportType}, Name: {ClientName}",
            context.ConnectionId, client.Id, transportType, client.Name);

        return Result.Success("Client registered successfully");
    }

    /// <inheritdoc />
    public async Task<Result<StreamFlowInvokeResponse>> InvokeMethodAsync(
        StreamFlowMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a TaskCompletionSource for the response
            var tcs = new TaskCompletionSource<StreamFlowMessage>();

            // Enqueue the method call
            var queued = await _cachingService.MessageQueue.TryEnqueueMethodCallAsync(
                message.RequestId, tcs, cancellationToken);

            if (!queued)
            {
                _logger.LogWarning("Failed to queue method call. RequestId: {RequestId}", message.RequestId);
                return Result<StreamFlowInvokeResponse>.Failure("Failed to queue method call", 500);
            }

            // Wait for response with timeout
            var timeout = TimeSpan.FromSeconds(30);
            var responseTask = tcs.Task;
            var completedTask = await Task.WhenAny(responseTask, Task.Delay(timeout, cancellationToken));

            if (completedTask != responseTask)
            {
                _logger.LogWarning("Method invocation timed out. RequestId: {RequestId}", message.RequestId);
                return Result<StreamFlowInvokeResponse>.Failure("Method invocation timed out", 408);
            }

            var responseMessage = await responseTask;

            var response = new StreamFlowInvokeResponse
            {
                HttpStatusCode = responseMessage.ResponseStatusCode,
                Message = responseMessage.Message,
                Response = responseMessage.Data
            };

            return Result<StreamFlowInvokeResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking method. RequestId: {RequestId}", message.RequestId);
            return Result<StreamFlowInvokeResponse>.Failure("An error occurred invoking the method", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> InvokeResponseAsync(
        StreamFlowMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Find the waiting TaskCompletionSource and complete it
            // Note: The actual TCS lookup and completion would be done by the processor
            // For now, we just log the response received
            _logger.LogInformation("Method response received. RequestId: {RequestId}", message.RequestId);

            return Result.Success("Response received");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing method response. RequestId: {RequestId}", message.RequestId);
            return Result.Failure("An error occurred processing the response", 500);
        }
    }

    /// <inheritdoc />
    public async Task DequeueMessagesAsync(
        StreamFlowClient client,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        // Dequeuing is handled automatically by StreamFlowProcessor
        // This method can be used to trigger immediate delivery of queued messages
        // for the newly connected client
        
        _logger.LogDebug("Dequeue request for client. ClientId: {ClientId}", client.Id);
        
        // The StreamFlowProcessor continuously processes the message queue
        // Messages for this client will be delivered when the processor finds them
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handles direct message delivery with load balancing and queueing.
    /// Preserves Channel-based optimization from Phase 3.3.
    /// </summary>
    private async Task HandleDirectMessageAsync(
        StreamFlowMessage message,
        StreamFlowClient sender,
        CancellationToken cancellationToken)
    {
        var availableClients = _cachingService.Clients
            .Where(x => x.Value.Id == message.RecipientId)
            .Select(i => i.Value)
            .ToList();
        var count = availableClients.Count;

        StreamFlowClient currentClient = null;

        if (count > 1)
        {
            // Multiple clients available - use round-robin load balancing
            currentClient = SelectClientForLoadBalancing(availableClients, message.RecipientId);
        }
        else if (count == 1)
        {
            currentClient = availableClients.First();
        }

        if (currentClient != null)
        {
            // Client is online, deliver immediately
            _logger.LogInformation(
                "Direct message sent. ExchangeType: {ExchangeType}, RequestId: {RequestId}, Sender: {SenderName} -> Recipient: {RecipientName}, Status: {StatusCode}",
                message.ExchangeType, message.RequestId, sender.Name, currentClient.Name, message.ResponseStatusCode);

            await _hubContext.Clients.Client(currentClient.StreamId)
                .SendAsync(message.CommandName, message, cancellationToken);
            return;
        }

        // Client is not online - check if known and queue if enabled
        if (_cachingService.AbsoluteClients.All(x => x.Value.Id != message.RecipientId))
        {
            _logger.LogWarning(
                "Invalid recipient for message. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                message.RequestId, sender.Name, message.RecipientId);
            return;
        }

        if (!_configuration.QueueMessages)
        {
            _logger.LogInformation(
                "Message queueing disabled. Message dropped. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                message.RequestId, sender.Name, message.RecipientId);
            return;
        }

        // Queue the message using channels with backpressure
        try
        {
            var queued = await _cachingService.MessageQueue.TryEnqueueMessageAsync(message, cancellationToken);

            if (queued)
            {
                _logger.LogInformation(
                    "Message queued for offline recipient. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                    message.RequestId, sender.Name, message.RecipientId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to queue message (channel closed). RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                    message.RequestId, sender.Name, message.RecipientId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Message queueing cancelled. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}",
                message.RequestId, sender.Name, message.RecipientId);
        }
    }

    /// <summary>
    /// Selects a client for load balancing using round-robin strategy.
    /// Preserves Phase 3.3 optimization.
    /// </summary>
    private StreamFlowClient SelectClientForLoadBalancing(List<StreamFlowClient> availableClients, string recipientId)
    {
        var count = availableClients.Count;
        var cachedClient = _cachingService.LatestClients
            .Select(i => i.Value)
            .FirstOrDefault(x => x.Id == recipientId);

        StreamFlowClient selectedClient;

        if (cachedClient is null)
        {
            // No cached client - use first available
            selectedClient = availableClients[0];

            // Add to cache with retry
            int attempts = 0;
            const int maxAttempts = 100;
            while (attempts < maxAttempts)
            {
                if (_cachingService.LatestClients.TryAdd(_cachingService.LatestClients.Count, selectedClient))
                {
                    break;
                }
                attempts++;
            }

            if (attempts >= maxAttempts)
            {
                _logger.LogWarning("Failed to cache latest client after {MaxAttempts} attempts. ClientId: {ClientId}",
                    maxAttempts, selectedClient.Id);
            }
        }
        else
        {
            // Select next client in round-robin fashion
            var cachedClientIndex = availableClients.IndexOf(cachedClient);
            selectedClient = (cachedClientIndex + 1) >= count
                ? availableClients[0]
                : availableClients[cachedClientIndex + 1];

            // Remove old cache entry with retry
            var tmpIndex = _cachingService.LatestClients.FirstOrDefault(i => i.Value.Id == cachedClient.Id);
            if (tmpIndex.Key != 0 || tmpIndex.Value != null)
            {
                int removeAttempts = 0;
                const int maxRemoveAttempts = 100;
                while (removeAttempts < maxRemoveAttempts)
                {
                    if (_cachingService.LatestClients.TryRemove(tmpIndex.Key, out _))
                    {
                        break;
                    }
                    removeAttempts++;
                }
            }

            // Add new cache entry with retry
            int addAttempts = 0;
            const int maxAddAttempts = 100;
            while (addAttempts < maxAddAttempts)
            {
                if (_cachingService.LatestClients.TryAdd(0, selectedClient))
                {
                    break;
                }
                addAttempts++;
            }

            if (addAttempts >= maxAddAttempts)
            {
                _logger.LogWarning("Failed to update latest client cache after {MaxAttempts} attempts. ClientId: {ClientId}",
                    maxAddAttempts, selectedClient.Id);
            }
        }

        return selectedClient;
    }

    /// <summary>
    /// Generates a unique client key using atomic increment.
    /// More efficient and reliable than random number generation with retry.
    /// </summary>
    private static long GenerateUniqueClientKey()
    {
        return Interlocked.Increment(ref _clientKeyCounter);
    }

    /// <summary>
    /// Remembers client in the absolute clients collection for reconnection tracking.
    /// </summary>
    private void RememberClient(StreamFlowClient client, HubCallerContext context)
    {
        if (_cachingService.AbsoluteClients.All(i => i.Value.Id != client.Id))
        {
            // New client - add to absolute clients
            int attempts = 0;
            const int maxAttempts = 100;
            bool added = false;

            while (attempts < maxAttempts && !added)
            {
                added = _cachingService.AbsoluteClients.TryAdd(
                    _cachingService.AbsoluteClients.Count,
                    new StreamFlowClient
                    {
                        StreamId = context.ConnectionId,
                        Id = client.Id,
                        Name = client.Name,
                        Queue = client.Queue,
                        ConnectedAt = DateTime.UtcNow
                    });
                attempts++;
            }

            if (added)
            {
                _logger.LogDebug("Client added to absolute clients. ClientId: {ClientId}", client.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to add client to absolute clients after {MaxAttempts} attempts. ClientId: {ClientId}",
                    maxAttempts, client.Id);
            }
        }
        else
        {
            // Existing client reconnecting - update connection ID
            var existingClient = _cachingService.AbsoluteClients.FirstOrDefault(i => i.Value.Id == client.Id);
            if (existingClient.Value != null)
            {
                existingClient.Value.StreamId = context.ConnectionId;
                existingClient.Value.ConnectedAt = DateTime.UtcNow;
                _logger.LogDebug("Updated connection ID for existing client. ClientId: {ClientId}", client.Id);
            }
        }
    }
}
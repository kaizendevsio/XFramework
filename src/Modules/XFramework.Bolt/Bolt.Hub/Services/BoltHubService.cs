using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Bolt.Domain.Shared.BusinessObjects;
using Bolt.Domain.Shared.Enums;
using Bolt.Hub.Hubs;
using Bolt.Hub.Interfaces;
using XFramework.Core.Loggers;
using System.Collections.Concurrent;
using XFramework.Domain.Shared.Configurations;

namespace Bolt.Hub.Services;

/// <summary>
/// Service for Bolt SignalR messaging operations.
/// Consolidates handler logic from MediatR handlers into direct service methods.
/// Preserves all Channel-based queueing and performance optimizations.
/// </summary>
public sealed class BoltHubService : IBoltHubService
{
    private readonly ICachingService _cachingService;
    private readonly IHubContext<MessageQueueHub> _hubContext;
    private readonly BoltConfiguration _configuration;
    private readonly ILogger<BoltHubService> _logger;
    private readonly DeadLetterQueue _dlq;
    private static long _clientKeyCounter = 100000000;
    private readonly ConcurrentDictionary<string, int> _roundRobinIndex = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<BoltMessage>> _pendingInvocations = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _processedMessages = new();
    private readonly Timer _dedupCleanupTimer;

    public BoltHubService(
        ICachingService cachingService,
        IHubContext<MessageQueueHub> hubContext,
        BoltConfiguration configuration,
        ILogger<BoltHubService> logger,
        DeadLetterQueue dlq)
    {
        _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dlq = dlq ?? throw new ArgumentNullException(nameof(dlq));
        _dedupCleanupTimer = new Timer(_ => CleanupDedupCache(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <inheritdoc />
    public async Task<Result> PushMessageAsync(
        BoltMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_processedMessages.TryAdd(message.RequestId, DateTime.UtcNow))
        {
            _logger.LogDebug("Duplicate message ignored: {RequestId}", message.RequestId);
            return Result.Success("Duplicate message ignored");
        }

        // Check if Client is Registered (O(1) via reverse index)
        BoltHubClient? senderClient = null;
        if (_cachingService.ClientKeyByStreamId.TryGetValue(context.ConnectionId, out var senderKey))
            _cachingService.Clients.TryGetValue(senderKey, out senderClient);

        if (senderClient == null)
        {
            _logger.BoltClientUnauthorized(context.ConnectionId);
            
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
                    _logger.BoltFanOutSent(message.RequestId.ToString(), senderClient.Name);
                    break;

                case MessageExchangeType.Direct:
                    await HandleDirectMessageAsync(message, senderClient, cancellationToken);
                    break;

                case MessageExchangeType.Topic:
                    await _hubContext.Clients.Group(message.Topic)
                        .SendAsync(message.CommandName, message, cancellationToken: cancellationToken);
                    _logger.BoltTopicSent(message.RequestId.ToString(), message.Topic, senderClient.Name);
                    break;

                default:
                    return Result.Failure(
                        $"Unsupported exchange type: {message.ExchangeType}", 400);
            }

            return Result.Success("Message pushed successfully");
        }
        catch (Exception ex)
        {
            _logger.BoltPushError(message.RequestId.ToString(), ex);
            return Result.Failure("An error occurred processing the message", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> RegisterClientAsync(
        BoltHubClient client,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        var clientKey = GenerateUniqueClientKey();
        var clientInfo = new BoltHubClient
        {
            StreamId = context.ConnectionId,
            Id = client.Id,
            Name = client.Name,
            Queue = client.Queue,
            ConnectedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
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
            _logger.BoltClientRegistrationFailed(maxAttempts, context.ConnectionId, client.Id);
            return Result.Failure("Failed to register client", 500);
        }

        // Populate reverse indexes for O(1) lookups
        _cachingService.ClientKeyByStreamId[context.ConnectionId] = (int)clientKey;
        _cachingService.ClientsByServiceId.AddOrUpdate(
            client.Id,
            _ => new ConcurrentBag<int> { (int)clientKey },
            (_, bag) => { bag.Add((int)clientKey); return bag; });

        RememberClient(client, context);

        var transportType = context.Features.Get<IHttpTransportFeature>()?.TransportType.ToString() ?? "Unknown";
        _logger.BoltClientRegistered(context.ConnectionId, client.Id, transportType, client.Name);

        return Result.Success("Client registered successfully");
    }

    /// <inheritdoc />
    public async Task<Result<BoltInvokeResponse>> InvokeMethodAsync(
        BoltMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<BoltMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingInvocations[message.RequestId] = tcs;

        try
        {
            // Find the recipient client
            var availableClients = GetClientsByServiceId(message.RecipientId);
            if (availableClients.Count == 0)
            {
                _logger.BoltMethodCallQueueFailed(message.RequestId.ToString());
                return Result<BoltInvokeResponse>.Failure("No clients available for recipient", 404);
            }

            var recipient = availableClients.Count > 1
                ? SelectClientForLoadBalancing(availableClients, message.RecipientId)
                : availableClients[0];

            // Send the message to the recipient
            await _hubContext.Clients.Client(recipient.StreamId)
                .SendAsync(message.CommandName, message, cancellationToken);

            // Wait for response with CancellationToken-based timeout (cheaper than Task.Delay allocation)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_configuration.RpcTimeoutSeconds > 0 ? _configuration.RpcTimeoutSeconds : 30));

            try
            {
                var responseMessage = await tcs.Task.WaitAsync(timeoutCts.Token);

                var response = new BoltInvokeResponse
                {
                    HttpStatusCode = responseMessage.ResponseStatusCode,
                    Message = responseMessage.Message,
                    Response = responseMessage.Data
                };

                return Result<BoltInvokeResponse>.Success(response);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.BoltMethodInvocationTimeout(message.RequestId.ToString());
                return Result<BoltInvokeResponse>.Failure("Method invocation timed out", 408);
            }
        }
        catch (Exception ex)
        {
            _logger.BoltMethodInvocationError(message.RequestId.ToString(), ex);
            return Result<BoltInvokeResponse>.Failure("An error occurred invoking the method", 500);
        }
        finally
        {
            _pendingInvocations.TryRemove(message.RequestId, out _);
        }
    }

    /// <inheritdoc />
    public async Task<Result> InvokeResponseAsync(
        BoltMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_pendingInvocations.TryGetValue(message.RequestId, out var tcs))
            {
                tcs.TrySetResult(message);
                _logger.BoltMethodResponseReceived(message.RequestId.ToString());
                return Result.Success("Response received");
            }

            _logger.BoltMethodResponseError(message.RequestId.ToString(),
                new InvalidOperationException($"No pending invocation found for RequestId {message.RequestId}"));
            return Result.Failure("No pending invocation found for this request", 404);
        }
        catch (Exception ex)
        {
            _logger.BoltMethodResponseError(message.RequestId.ToString(), ex);
            return Result.Failure("An error occurred processing the response", 500);
        }
    }

    /// <inheritdoc />
    public async Task DequeueMessagesAsync(
        BoltHubClient client,
        HubCallerContext context,
        CancellationToken cancellationToken = default)
    {
        // Dequeuing is handled automatically by BoltProcessor
        // This method can be used to trigger immediate delivery of queued messages
        // for the newly connected client
        
        _logger.BoltDequeueRequest(client.Id);
        
        // The BoltProcessor continuously processes the message queue
        // Messages for this client will be delivered when the processor finds them
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handles direct message delivery with load balancing and queueing.
    /// Preserves Channel-based optimization from Phase 3.3.
    /// </summary>
    private async Task HandleDirectMessageAsync(
        BoltMessage message,
        BoltHubClient sender,
        CancellationToken cancellationToken)
    {
        var allClients = GetClientsByServiceId(message.RecipientId);

        // Health-aware routing: filter out circuit-open clients
        var availableClients = allClients.Where(c => !c.IsCircuitOpen).ToList();
        if (availableClients.Count == 0 && allClients.Count > 0)
        {
            // All clients are circuit-open; fall back to all clients rather than blocking completely
            availableClients = allClients;
        }

        var count = availableClients.Count;

        BoltHubClient currentClient = null;

        if (count > 1)
        {
            // Multiple clients available - use round-robin load balancing
            currentClient = SelectClientForLoadBalancing(availableClients, message.RecipientId);
        }
        else if (count == 1)
        {
            currentClient = availableClients[0];
        }

        if (currentClient != null)
        {
            // Client is online, deliver immediately
            _logger.BoltDirectSent(message.ExchangeType.ToString(), message.RequestId.ToString(),
                sender.Name, currentClient.Name, (int)message.ResponseStatusCode);

            try
            {
                await _hubContext.Clients.Client(currentClient.StreamId)
                    .SendAsync(message.CommandName, message, cancellationToken);

                currentClient.LastSeenAt = DateTime.UtcNow;
                Interlocked.Increment(ref currentClient.SuccessCount);
            }
            catch
            {
                Interlocked.Increment(ref currentClient.FailureCount);
                currentClient.LastFailureAt = DateTime.UtcNow;
                if (currentClient.FailureCount > 5 && currentClient.FailureCount > currentClient.SuccessCount)
                {
                    currentClient.IsCircuitOpen = true;
                }

                throw;
            }

            return;
        }

        // Client is not online - check if known and queue if enabled
        if (!_cachingService.AbsoluteClientKeyByServiceId.ContainsKey(message.RecipientId))
        {
            _logger.BoltInvalidRecipient(message.RequestId.ToString(), sender.Name, message.RecipientId);
            _dlq.Enqueue(message, "InvalidRecipient");
            return;
        }

        if (!_configuration.QueueMessages)
        {
            _logger.BoltMessageQueuingDisabled(message.RequestId.ToString(), sender.Name, message.RecipientId);
            _dlq.Enqueue(message, "QueueDisabled");
            return;
        }

        // Queue the message using channels with backpressure
        try
        {
            var queued = await _cachingService.MessageQueue.TryEnqueueMessageAsync(message, cancellationToken);

            if (queued)
            {
                _logger.BoltMessageQueued(message.RequestId.ToString(), sender.Name, message.RecipientId);
            }
            else
            {
                _logger.BoltMessageQueueFailed(message.RequestId.ToString(), sender.Name, message.RecipientId);
                _dlq.Enqueue(message, "QueueFull");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.BoltMessageQueueCancelled(message.RequestId.ToString(), sender.Name, message.RecipientId);
        }
    }

    /// <summary>
    /// Selects a client for load balancing using atomic round-robin strategy.
    /// </summary>
    private BoltHubClient SelectClientForLoadBalancing(IReadOnlyList<BoltHubClient> clients, string recipientId)
    {
        var idx = _roundRobinIndex.AddOrUpdate(recipientId, 0, (_, prev) => prev + 1);
        return clients[(int)((uint)idx % clients.Count)];
    }

    /// <summary>
    /// Gets all connected clients for a given service/recipient ID using the reverse index.
    /// Falls back to O(n) scan if the reverse index is empty (defensive).
    /// </summary>
    private List<BoltHubClient> GetClientsByServiceId(string serviceId)
    {
        if (_cachingService.ClientsByServiceId.TryGetValue(serviceId, out var clientKeys))
        {
            var result = new List<BoltHubClient>();
            foreach (var key in clientKeys)
            {
                if (_cachingService.Clients.TryGetValue(key, out var client))
                {
                    result.Add(client);
                }
            }
            return result;
        }
        return [];
    }

    /// <summary>
    /// Generates a unique client key using atomic increment.
    /// More efficient and reliable than random number generation with retry.
    /// </summary>
    private static long GenerateUniqueClientKey()
    {
        return Interlocked.Increment(ref _clientKeyCounter);
    }

    private void CleanupDedupCache()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        foreach (var (key, time) in _processedMessages)
        {
            if (time < cutoff)
                _processedMessages.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Remembers client in the absolute clients collection for reconnection tracking.
    /// Uses O(1) reverse index instead of O(n) scans.
    /// </summary>
    private void RememberClient(BoltHubClient client, HubCallerContext context)
    {
        if (_cachingService.AbsoluteClientKeyByServiceId.TryGetValue(client.Id, out var existingKey)
            && _cachingService.AbsoluteClients.TryGetValue(existingKey, out var existingClient))
        {
            // Existing client reconnecting - update connection ID and last seen
            existingClient.StreamId = context.ConnectionId;
            existingClient.ConnectedAt = DateTime.UtcNow;
            existingClient.LastSeenAt = DateTime.UtcNow;
            _logger.BoltClientConnectionUpdated(client.Id);
        }
        else
        {
            // New client - add to absolute clients
            var newClient = new BoltHubClient
            {
                StreamId = context.ConnectionId,
                Id = client.Id,
                Name = client.Name,
                Queue = client.Queue,
                ConnectedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };

            var key = (int)GenerateUniqueClientKey();
            if (_cachingService.AbsoluteClients.TryAdd(key, newClient))
            {
                _cachingService.AbsoluteClientKeyByServiceId[client.Id] = key;
                _logger.BoltClientAddedToAbsolute(client.Id);
            }
            else
            {
                _logger.BoltAbsoluteClientAddFailed(1, client.Id);
            }
        }
    }
}
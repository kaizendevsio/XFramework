using System.Net;
using System.Collections.Concurrent;
using Bolt.Client;
using Bolt.Domain.Shared.Contracts.Requests;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Security;

namespace XFramework.Integration.Drivers;

/// <summary>
/// IMessageBusWrapper implementation backed by the Bolt thin protocol client.
/// Replaces BoltDriverSignalR for services migrated off SignalR.
///
/// The recipient parameter in Send methods is the target service name (looked up in
/// BoltConfiguration.Targets) or a direct client ID. The request type name is used
/// as the Bolt command hash for routing on the hub.
/// </summary>
public sealed class BoltDriver : IMessageBusWrapper, IDisposable
{
    private readonly BoltClient _client;
    private readonly BoltConfiguration _config;
    private readonly IServiceTokenProvider _serviceTokenProvider;
    private readonly IActorAccessTokenProvider _actorAccessTokenProvider;
    private readonly ILogger<BoltDriver> _logger;
    private readonly Action _disconnectedHandler;
    private readonly Action _reconnectingHandler;
    private readonly Action _reconnectedHandler;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _legacySubscriptions = new();
    private readonly object _subscriptionGate = new();
    private readonly Dictionary<long, Task> _activeSubscriptions = [];
    private readonly CancellationTokenSource _subscriptionLifetime = new();
    private long _nextSubscriptionId;
    private bool _disposed;

    public bool IsConnected => _client.IsConnected;
    public Action OnReconnected { get; set; } = () => { };
    public Action OnReconnecting { get; set; } = () => { };
    public Action OnDisconnected { get; set; } = () => { };

    public BoltDriver(
        BoltClient client,
        IOptions<BoltConfiguration> config,
        IServiceTokenProvider serviceTokenProvider,
        IActorAccessTokenProvider actorAccessTokenProvider,
        ILogger<BoltDriver> logger)
    {
        _client = client;
        _config = config.Value;
        _serviceTokenProvider = serviceTokenProvider;
        _actorAccessTokenProvider = actorAccessTokenProvider;
        _logger = logger;
        _disconnectedHandler = () => OnDisconnected();
        _reconnectingHandler = () => OnReconnecting();
        _reconnectedHandler = () => OnReconnected();
        _client.Disconnected += _disconnectedHandler;
        _client.Reconnecting += _reconnectingHandler;
        _client.Reconnected += _reconnectedHandler;
    }

    public void Dispose()
    {
        Task[] activeSubscriptions;
        lock (_subscriptionGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            activeSubscriptions = _activeSubscriptions.Values.ToArray();
        }

        _client.Disconnected -= _disconnectedHandler;
        _client.Reconnecting -= _reconnectingHandler;
        _client.Reconnected -= _reconnectedHandler;

        _subscriptionLifetime.Cancel();

        foreach (var subscription in _legacySubscriptions.Values)
        {
            subscription.Cancel();
            subscription.Dispose();
        }

        _legacySubscriptions.Clear();
        Task.WhenAll(activeSubscriptions).GetAwaiter().GetResult();
        _subscriptionLifetime.Dispose();
    }

    public async Task<bool> Connect()
    {
        try
        {
            if (!_client.IsConnected)
                await _client.ConnectWithRetryAsync();
            return _client.IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BoltDriver failed to connect");
            return false;
        }
    }

    public Task StartClientEventListener(string topic) => Task.CompletedTask;

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, string recipient, CancellationToken ct = default)
        where TRequest : class, IHasRequestServer
    {
        var (recipientId, payload) = await CreateInvocationAsync(request, recipient, ct);
        var (status, responsePayload) = await _client.InvokeAsync(recipientId, typeof(TRequest).Name, payload, ct);
        return DeserializeCmdResponse(status, responsePayload);
    }

    public async Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request, string recipient, CancellationToken ct = default)
        where TRequest : class, IHasRequestServer
    {
        var (recipientId, payload) = await CreateInvocationAsync(request, recipient, ct);
        var (status, responsePayload) = await _client.InvokeAsync(recipientId, typeof(TRequest).Name, payload, ct);
        return DeserializeCmdResponse<TResponse>(status, responsePayload);
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, string recipient, CancellationToken ct = default)
        where TRequest : class, IHasRequestServer
    {
        var (recipientId, payload) = await CreateInvocationAsync(request, recipient, ct);
        var (status, responsePayload) = await _client.InvokeAsync(recipientId, typeof(TRequest).Name, payload, ct);
        return DeserializeQueryResponse<TResponse>(status, responsePayload);
    }

    public async Task PublishAsync<TModel>(string eventName, string topic, TModel? data)
        where TModel : class, IHasRequestServer
    {
        if (data is not null)
        {
            data.Metadata ??= new RequestMetadata();
            data.Metadata.OperationName = eventName;
            data.Metadata.RequestId ??= Guid.NewGuid();
        }
        await _client.PublishAsync(topic, data, durable: false);
    }

    public async Task PublishAsync<TModel>(string eventName, string topic, TModel? data, bool durable)
        where TModel : class
    {
        await _client.PublishAsync(topic, data, durable);
    }

    public Task PublishAsync(string eventName, string topic)
        => _client.PublishAsync<object?>(topic, null, durable: false).AsTask();

    public Task Subscribe<TResponse>(BoltSubscriptionRequest<TResponse> request)
        where TResponse : class
    {
        var cts = new CancellationTokenSource();
        if (_legacySubscriptions.TryRemove(request.Name, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }
        _legacySubscriptions[request.Name] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in _client.SubscribeAsync<TResponse>(request.Name, cts.Token))
                    request.OnInvoke?.Invoke(item);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transient subscription error: topic={Topic}", request.Name);
            }
            finally
            {
                _legacySubscriptions.TryRemove(new KeyValuePair<string, CancellationTokenSource>(request.Name, cts));
                cts.Dispose();
            }
        });
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<TResponse>(string topic, Func<TResponse, Task> handler, CancellationToken ct = default)
        where TResponse : class
        => SubscribeAsync(topic, handler, actorAccessToken: null, ct);

    public Task SubscribeAsync<TResponse>(
        string topic,
        Func<TResponse, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default)
        where TResponse : class =>
        SubscribeAsync(
            topic,
            handler,
            _ => ValueTask.FromResult(actorAccessToken),
            ct);

    public Task SubscribeAsync<TResponse>(
        string topic,
        Func<TResponse, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default)
        where TResponse : class
    {
        return StartTrackedSubscription(async subscriptionCt =>
        {
            try
            {
                await foreach (var item in _client.SubscribeAsync<TResponse>(
                                   topic,
                                   subscriptionCt,
                                   actorAccessTokenProvider))
                {
                    try
                    {
                        await handler(item);
                    }
                    catch (OperationCanceledException) when (subscriptionCt.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Transient handler threw. topic={Topic}", topic);
                    }
                }
            }
            catch (OperationCanceledException) when (subscriptionCt.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transient subscription error: topic={Topic}", topic);
            }
        }, ct);
    }

    public async Task Unsubscribe(BoltSubscriptionRequest request)
    {
        if (_legacySubscriptions.TryRemove(request.Name, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        try
        {
            await _client.UnsubscribeAsync(request.Name);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Bolt unsubscribe failed. topic={Topic}", request.Name);
        }
    }

    public Task SubscribeDurableAsync<TResponse>(string topic, string subscriberId, Func<TResponse, Task> handler, CancellationToken ct = default)
        where TResponse : class
        => SubscribeDurableAsync(topic, subscriberId, handler, actorAccessToken: null, ct);

    public Task SubscribeDurableAsync<TResponse>(
        string topic,
        string subscriberId,
        Func<TResponse, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default)
        where TResponse : class =>
        SubscribeDurableAsync(
            topic,
            subscriberId,
            handler,
            _ => ValueTask.FromResult(actorAccessToken),
            ct);

    public Task SubscribeDurableAsync<TResponse>(
        string topic,
        string subscriberId,
        Func<TResponse, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default)
        where TResponse : class
    {
        return StartTrackedSubscription(async subscriptionCt =>
        {
            try
            {
                await foreach (var msg in _client.SubscribeDurableAsync<TResponse>(
                                   topic,
                                   subscriberId,
                                   subscriptionCt,
                                   actorAccessTokenProvider))
                {
                    try
                    {
                        await handler(msg.Payload);
                        await msg.AckAsync(subscriptionCt);
                    }
                    catch (OperationCanceledException) when (subscriptionCt.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Durable handler threw; not acking. topic={Topic} subscriber={Subscriber} seq={Seq}", topic, subscriberId, msg.Sequence);
                    }
                }
            }
            catch (OperationCanceledException) when (subscriptionCt.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Durable subscription error: topic={Topic} subscriber={Subscriber}", topic, subscriberId);
            }
        }, ct);
    }

    private Task StartTrackedSubscription(
        Func<CancellationToken, Task> subscribe,
        CancellationToken callerToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        long subscriptionId;
        lock (_subscriptionGate)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BoltDriver));

            subscriptionId = ++_nextSubscriptionId;
            _activeSubscriptions.Add(subscriptionId, completion.Task);
        }

        _ = Task.Run(async () =>
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                callerToken,
                _subscriptionLifetime.Token);
            try
            {
                await subscribe(linkedCts.Token);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tracked Bolt subscription failed.");
            }
            finally
            {
                lock (_subscriptionGate)
                    _activeSubscriptions.Remove(subscriptionId);

                completion.TrySetResult();
            }
        });

        return Task.CompletedTask;
    }

    private async Task<(string RecipientId, byte[] Payload)> CreateInvocationAsync<TRequest>(
        TRequest request,
        string recipient,
        CancellationToken ct)
        where TRequest : IHasRequestServer
    {
        request.Metadata ??= new RequestMetadata();
        request.Metadata.OperationName ??= typeof(TRequest).Name;
        request.Metadata.RequestId ??= Guid.NewGuid();

        var audience = ResolveAudience(recipient);
        var envelope = new BoltInvocationEnvelope
        {
            Payload = MemoryPackSerializer.Serialize(request),
            ActorAccessToken = await _actorAccessTokenProvider.GetTokenAsync(ct),
            ServiceAccessToken = await _serviceTokenProvider.GetTokenAsync(audience, null, ct)
        };

        return (ResolveRecipientId(recipient), MemoryPackSerializer.Serialize(envelope));
    }

    private static string ResolveAudience(string recipient)
    {
        var trimmed = recipient.Trim();
        var canonical = XFrameworkServiceNames.All.FirstOrDefault(name =>
            string.Equals(name, trimmed, StringComparison.Ordinal) ||
            string.Equals(name.ToSha256(), trimmed, StringComparison.OrdinalIgnoreCase));

        return canonical ?? trimmed;
    }

    private static string ResolveRecipientId(string recipient)
    {
        var trimmed = recipient.Trim();
        var canonical = XFrameworkServiceNames.All.FirstOrDefault(name =>
            string.Equals(name, trimmed, StringComparison.Ordinal) ||
            string.Equals(name.ToSha256(), trimmed, StringComparison.OrdinalIgnoreCase));

        return canonical is null ? trimmed : canonical.ToSha256();
    }

    private static CmdResponse DeserializeCmdResponse(HttpStatusCode status, ReadOnlyMemory<byte> responsePayload)
    {
        if (!responsePayload.IsEmpty)
        {
            try
            {
                var wrapped = MemoryPackSerializer.Deserialize<CmdResponse>(responsePayload.Span);
                if (wrapped is not null)
                    return wrapped;
            }
            catch (MemoryPackSerializationException)
            {
                // Older handlers returned no command envelope payload.
            }
        }

        return new CmdResponse { HttpStatusCode = status, Message = status.ToString() };
    }

    private static CmdResponse<TResponse> DeserializeCmdResponse<TResponse>(
        HttpStatusCode status,
        ReadOnlyMemory<byte> responsePayload)
    {
        if (responsePayload.IsEmpty)
            return new CmdResponse<TResponse> { HttpStatusCode = status, Message = status.ToString() };

        try
        {
            var wrapped = MemoryPackSerializer.Deserialize<CmdResponse<TResponse>>(responsePayload.Span);
            if (wrapped is not null)
                return wrapped;
        }
        catch (MemoryPackSerializationException)
        {
            // Fall back for legacy handlers that serialized only TResponse.
        }

        var response = MemoryPackSerializer.Deserialize<TResponse>(responsePayload.Span);
        return new CmdResponse<TResponse>
        {
            HttpStatusCode = status,
            Message = status.ToString(),
            Response = response
        };
    }

    private static QueryResponse<TResponse> DeserializeQueryResponse<TResponse>(
        HttpStatusCode status,
        ReadOnlyMemory<byte> responsePayload)
    {
        if (responsePayload.IsEmpty)
            return new QueryResponse<TResponse> { HttpStatusCode = status, Message = status.ToString() };

        try
        {
            var wrapped = MemoryPackSerializer.Deserialize<QueryResponse<TResponse>>(responsePayload.Span);
            if (wrapped is not null)
                return wrapped;
        }
        catch (MemoryPackSerializationException)
        {
            // Fall back for legacy handlers that serialized only TResponse.
        }

        var response = MemoryPackSerializer.Deserialize<TResponse>(responsePayload.Span);
        return new QueryResponse<TResponse>
        {
            HttpStatusCode = status,
            Message = status.ToString(),
            Response = response
        };
    }
}

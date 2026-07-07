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
public sealed class BoltDriver : IMessageBusWrapper
{
    private readonly BoltClient _client;
    private readonly BoltConfiguration _config;
    private readonly IServiceTokenProvider _serviceTokenProvider;
    private readonly ILogger<BoltDriver> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _legacySubscriptions = new();

    public bool IsConnected => _client.IsConnected;
    public Action OnReconnected { get; set; } = () => { };
    public Action OnReconnecting { get; set; } = () => { };
    public Action OnDisconnected { get; set; } = () => { };

    public BoltDriver(
        BoltClient client,
        IOptions<BoltConfiguration> config,
        IServiceTokenProvider serviceTokenProvider,
        ILogger<BoltDriver> logger)
    {
        _client = client;
        _config = config.Value;
        _serviceTokenProvider = serviceTokenProvider;
        _logger = logger;
        _client.Disconnected += () => OnDisconnected();
        _client.Reconnecting += () => OnReconnecting();
        _client.Reconnected += () => OnReconnected();
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
        var recipientId = await EnrichMetadataAsync(request, recipient, ct);
        var payload = MemoryPackSerializer.Serialize(request);
        var (status, responsePayload) = await _client.InvokeAsync(recipientId, typeof(TRequest).Name, payload, ct);
        return DeserializeCmdResponse(status, responsePayload);
    }

    public async Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request, string recipient, CancellationToken ct = default)
        where TRequest : class, IHasRequestServer
    {
        var recipientId = await EnrichMetadataAsync(request, recipient, ct);
        var payload = MemoryPackSerializer.Serialize(request);
        var (status, responsePayload) = await _client.InvokeAsync(recipientId, typeof(TRequest).Name, payload, ct);
        return DeserializeCmdResponse<TResponse>(status, responsePayload);
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, string recipient, CancellationToken ct = default)
        where TRequest : class, IHasRequestServer
    {
        var recipientId = await EnrichMetadataAsync(request, recipient, ct);
        var payload = MemoryPackSerializer.Serialize(request);
        var (status, responsePayload) = await _client.InvokeAsync(recipientId, typeof(TRequest).Name, payload, ct);
        return DeserializeQueryResponse<TResponse>(status, responsePayload);
    }

    public async Task PublishAsync<TModel>(string eventName, string topic, TModel? data)
        where TModel : class, IHasRequestServer
    {
        if (data is not null)
        {
            data.Metadata ??= new RequestMetadata();
            data.Metadata.Name = _config.ClientName ?? string.Empty;
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
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in _client.SubscribeAsync<TResponse>(topic, ct, actorAccessTokenProvider))
                {
                    try
                    {
                        await handler(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Transient handler threw. topic={Topic}", topic);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transient subscription error: topic={Topic}", topic);
            }
        }, ct);
        return Task.CompletedTask;
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
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in _client.SubscribeDurableAsync<TResponse>(topic, subscriberId, ct, actorAccessTokenProvider))
                {
                    try
                    {
                        await handler(msg.Payload);
                        await msg.AckAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Durable handler threw; not acking. topic={Topic} subscriber={Subscriber} seq={Seq}", topic, subscriberId, msg.Sequence);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Durable subscription error: topic={Topic} subscriber={Subscriber}", topic, subscriberId);
            }
        }, ct);
        return Task.CompletedTask;
    }

    private async Task<string> EnrichMetadataAsync<TRequest>(TRequest request, string recipient, CancellationToken ct)
        where TRequest : IHasRequestServer
    {
        request.Metadata ??= new RequestMetadata();
        request.Metadata.Name = _config.ClientName ?? string.Empty;
        if (request.Metadata.TenantId == null && _config.ClientGuid.HasValue)
            request.Metadata.TenantId = _config.ClientGuid.Value;
        request.Metadata.RequestId ??= Guid.NewGuid();

        var audience = ResolveAudience(recipient);
        request.Metadata.ServiceAccessToken = await _serviceTokenProvider.GetTokenAsync(audience, null, ct);
        return ResolveRecipientId(recipient);
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

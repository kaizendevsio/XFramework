using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bolt.Client;
using Bolt.Domain.Shared.Contracts.Requests;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Abstractions.Wrappers;

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
    private readonly ILogger<BoltDriver> _logger;

    public bool IsConnected => _client.IsConnected;
    public Action OnReconnected { get; set; } = () => { };
    public Action OnReconnecting { get; set; } = () => { };
    public Action OnDisconnected { get; set; } = () => { };

    public BoltDriver(BoltClient client, IOptions<BoltConfiguration> config, ILogger<BoltDriver> logger)
    {
        _client = client;
        _config = config.Value;
        _logger = logger;
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

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = false,
        MaxDepth = 4,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private static JsonElement SafeSerializeToElement(object? obj)
    {
        if (obj is null) return EmptyObject;
        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(obj, _jsonOpts);
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch { return EmptyObject; }
    }

    private static string FriendlyTypeName<T>()
    {
        var type = typeof(T);
        if (!type.IsGenericType) return type.Name;
        var name = type.Name[..type.Name.IndexOf('`')];
        var args = string.Join(", ", type.GetGenericArguments().Select(a => a.Name));
        return $"{name}<{args}>";
    }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        EnrichMetadata(request);
        var command = FriendlyTypeName<TRequest>();
        var sw = Stopwatch.StartNew();
        var payload = MemoryPackSerializer.Serialize(request);
        var (status, _) = await _client.InvokeAsync(recipient, typeof(TRequest).Name, payload);
        sw.Stop();

        var level = (int)status >= 400 ? LogLevel.Warning : LogLevel.Debug;
        _logger.Log(level, "Bolt RPC {Command} -> {Recipient} | {Response.Status} in {Response.Elapsed}ms",
            command, recipient, (int)status, sw.ElapsedMilliseconds);

        return new CmdResponse { HttpStatusCode = status, Message = status.ToString() };
    }

    public async Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        EnrichMetadata(request);
        var command = FriendlyTypeName<TRequest>();
        var sw = Stopwatch.StartNew();
        var payload = MemoryPackSerializer.Serialize(request);
        var (status, responsePayload) = await _client.InvokeAsync(recipient, typeof(TRequest).Name, payload);
        sw.Stop();
        var response = responsePayload.IsEmpty ? default : MemoryPackSerializer.Deserialize<TResponse>(responsePayload.Span);

        var level = (int)status >= 400 ? LogLevel.Warning : LogLevel.Debug;
        _logger.Log(level,
            "Bolt RPC {Command} -> {Recipient} | {Response.Status} in {Response.Elapsed}ms | Request={Request} Response={Response}",
            command, recipient,
            (int)status, sw.ElapsedMilliseconds,
            SafeSerializeToElement(new { Size = payload.Length, Body = SafeSerializeToElement(request) }),
            SafeSerializeToElement(new { Status = (int)status, Elapsed = sw.ElapsedMilliseconds, Size = responsePayload.Length, Body = SafeSerializeToElement(response) }));

        return new CmdResponse<TResponse> { HttpStatusCode = status, Message = status.ToString(), Response = response };
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        EnrichMetadata(request);
        var command = FriendlyTypeName<TRequest>();
        var sw = Stopwatch.StartNew();
        var payload = MemoryPackSerializer.Serialize(request);
        var (status, responsePayload) = await _client.InvokeAsync(recipient, typeof(TRequest).Name, payload);
        sw.Stop();
        var response = responsePayload.IsEmpty ? default : MemoryPackSerializer.Deserialize<TResponse>(responsePayload.Span);

        var level = (int)status >= 400 ? LogLevel.Warning : LogLevel.Debug;
        _logger.Log(level,
            "Bolt RPC {Command} -> {Recipient} (query) | {Response.Status} in {Response.Elapsed}ms | Request={Request} Response={Response}",
            command, recipient,
            (int)status, sw.ElapsedMilliseconds,
            SafeSerializeToElement(new { Size = payload.Length, Body = SafeSerializeToElement(request) }),
            SafeSerializeToElement(new { Status = (int)status, Elapsed = sw.ElapsedMilliseconds, Size = responsePayload.Length, Body = SafeSerializeToElement(response) }));

        return new QueryResponse<TResponse> { HttpStatusCode = status, Message = status.ToString(), Response = response };
    }

    public async Task PublishAsync<TModel>(string eventName, string topic, TModel? data)
        where TModel : class, IHasRequestServer
    {
        if (data is not null) EnrichMetadata(data);
        await _client.PublishAsync(topic, data, durable: false);
    }

    public Task PublishAsync(string eventName, string topic)
        => _client.PublishAsync<object?>(topic, null, durable: false).AsTask();

    public Task Subscribe<TResponse>(BoltSubscriptionRequest<TResponse> request)
        where TResponse : class
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in _client.SubscribeAsync<TResponse>(request.Name))
                    request.OnInvoke?.Invoke(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transient subscription error: topic={Topic}", request.Name);
            }
        });
        return Task.CompletedTask;
    }

    public Task Unsubscribe(BoltSubscriptionRequest request)
    {
        // BoltClient's SubscribeAsync handles unsubscribe via CancellationToken cancellation.
        // The current IMessageBusWrapper.Unsubscribe signature doesn't expose a CTS, so this
        // is a no-op. Callers that need explicit unsubscribe should cancel the token passed
        // to Subscribe's underlying enumeration.
        return Task.CompletedTask;
    }

    public Task SubscribeDurableAsync<TResponse>(string topic, string subscriberId, Func<TResponse, Task> handler, CancellationToken ct = default)
        where TResponse : class
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in _client.SubscribeDurableAsync<TResponse>(topic, subscriberId, ct))
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

    private void EnrichMetadata<TRequest>(TRequest request) where TRequest : IHasRequestServer
    {
        request.Metadata ??= new RequestMetadata();
        if (string.IsNullOrEmpty(request.Metadata.Name))
            request.Metadata.Name = _config.ClientName ?? string.Empty;
        if (request.Metadata.TenantId == null && _config.ClientGuid.HasValue)
            request.Metadata.TenantId = _config.ClientGuid.Value;
    }
}

using System.Net;
using Bolt.Client;
using MemoryPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Extensions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Services;

namespace XFramework.Integration.ThinProtocol;

/// <summary>
/// Drop-in replacement for StreamFlowDriverSignalR using the thin binary protocol.
/// Implements IMessageBusWrapper so all service wrappers work unchanged.
/// </summary>
public class BoltDriver : IMessageBusWrapper
{
    private readonly Bolt.Client.BoltClient _client;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<BoltDriver> _logger;
    private readonly DeviceAgentProvider _deviceAgentProvider;

    private string? _clientIpAddress;
    private string? _clientName;
    private Guid? _tenantId;

    public bool IsConnected => _client.IsConnected;
    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }

    public BoltDriver(
        Bolt.Client.BoltClient client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<BoltDriver> logger,
        DeviceAgentProvider deviceAgentProvider)
    {
        _client = client;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
        _deviceAgentProvider = deviceAgentProvider;
    }

    public async Task<bool> Connect()
    {
        await _client.ConnectWithRetryAsync();
        return _client.IsConnected;
    }

    public Task StartClientEventListener(string topic)
    {
        // TODO: Implement pub/sub when needed
        return Task.CompletedTask;
    }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        await SetRequestMetadata(request);
        var commandName = typeof(TRequest).GetTypeFullName();
        var payload = MemoryPackSerializer.Serialize(request);

        try
        {
            var (statusCode, data) = await _client.InvokeAsync(recipient, commandName, payload);
            var response = MemoryPackSerializer.Deserialize<CmdResponse>(data.Span);
            return response ?? new CmdResponse { HttpStatusCode = statusCode };
        }
        catch (TimeoutException)
        {
            return new CmdResponse { HttpStatusCode = HttpStatusCode.RequestTimeout, Message = "RPC timeout" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendVoidAsync failed for {Request}", typeof(TRequest).Name);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.InternalServerError, Message = ex.Message };
        }
    }

    public async Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        await SetRequestMetadata(request);
        var commandName = typeof(TRequest).GetTypeFullName();
        var payload = MemoryPackSerializer.Serialize(request);

        try
        {
            var (statusCode, data) = await _client.InvokeAsync(recipient, commandName, payload);
            var response = MemoryPackSerializer.Deserialize<CmdResponse<TResponse>>(data.Span);
            return response ?? new CmdResponse<TResponse> { HttpStatusCode = statusCode };
        }
        catch (TimeoutException)
        {
            return new CmdResponse<TResponse> { HttpStatusCode = HttpStatusCode.RequestTimeout, Message = "RPC timeout" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendVoidAsync<{Response}> failed for {Request}", typeof(TResponse).Name, typeof(TRequest).Name);
            return new CmdResponse<TResponse> { HttpStatusCode = HttpStatusCode.InternalServerError, Message = ex.Message };
        }
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        await SetRequestMetadata(request);
        var commandName = typeof(TRequest).GetTypeFullName();
        var payload = MemoryPackSerializer.Serialize(request);

        try
        {
            var (statusCode, data) = await _client.InvokeAsync(recipient, commandName, payload);
            var response = MemoryPackSerializer.Deserialize<QueryResponse<TResponse>>(data.Span);
            return response ?? new QueryResponse<TResponse> { HttpStatusCode = statusCode };
        }
        catch (TimeoutException)
        {
            return new QueryResponse<TResponse> { HttpStatusCode = HttpStatusCode.RequestTimeout, Message = "RPC timeout" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendAsync<{Response}> failed for {Request}", typeof(TResponse).Name, typeof(TRequest).Name);
            return new QueryResponse<TResponse> { HttpStatusCode = HttpStatusCode.InternalServerError, Message = ex.Message };
        }
    }

    public Task PublishAsync<TModel>(string eventName, string topic, TModel? data) where TModel : class, IHasRequestServer
    {
        // TODO: Implement pub/sub
        _logger.LogWarning("PublishAsync not yet implemented in thin protocol");
        return Task.CompletedTask;
    }

    public Task PublishAsync(string eventName, string topic)
    {
        _logger.LogWarning("PublishAsync not yet implemented in thin protocol");
        return Task.CompletedTask;
    }

    public Task Subscribe<TResponse>(StreamFlowSubscriptionRequest<TResponse> request) where TResponse : class
    {
        _logger.LogWarning("Subscribe not yet implemented in thin protocol");
        return Task.CompletedTask;
    }

    public Task Unsubscribe(StreamFlowSubscriptionRequest request)
    {
        _logger.LogWarning("Unsubscribe not yet implemented in thin protocol");
        return Task.CompletedTask;
    }

    private async Task SetRequestMetadata<TRequest>(TRequest request)
        where TRequest : class, IHasRequestServer
    {
        _tenantId ??= Guid.TryParse(_configuration.GetValue<string>("Tenant:DefaultId"), out var appId)
            ? appId
            : throw new ArgumentNullException("Tenant:DefaultId is not set");

        _clientName ??= _configuration.GetValue<string>("StreamFlowConfiguration:ClientName")
                        ?? throw new ArgumentNullException("StreamFlowConfiguration:ClientName is not set");

        var existing = request.Metadata;
        request.Metadata = new RequestMetadata
        {
            DeviceAgent = _deviceAgentProvider.Name,
            TenantId = existing?.TenantId ?? _tenantId,
            Name = !string.IsNullOrEmpty(existing?.Name) ? existing.Name : _clientName,
            IpAddress = existing?.IpAddress ?? _clientIpAddress ?? "",
            RequestId = existing?.RequestId ?? Guid.NewGuid(),
            SessionId = existing?.SessionId ?? Guid.Empty
        };
    }
}

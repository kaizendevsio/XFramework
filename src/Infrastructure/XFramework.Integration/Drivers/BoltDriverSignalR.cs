using System.Collections.Concurrent;
using System.Text;
using MemoryPack;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Bolt.Domain.Shared.Abstractions;
using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Entity.Contracts.Responses;
using XFramework.Integration.Services;

namespace XFramework.Integration.Drivers;

public class BoltDriverSignalR : IMessageBusWrapper
{
    public ISignalRService SignalRService { get; set; }
    private IConfiguration Configuration { get; set; }
    private HttpClient HttpClient { get; set; }
    public CacheManager Cache { get; }
    public IHostEnvironment HostEnvironment { get; }
    private IHttpClientFactory HttpClientFactory { get; set; }
    private DeviceAgentProvider DeviceAgentProvider { get; set; }
    private ILogger<BoltDriverSignalR> Logger { get; }
    private string? ClientIpAddress { get; set; }
    private DateTime ClientIpAddressLastFailedFetch { get; set; }
    private TimeSpan ClientIpAddressFetchTimeout => DateTime.Now - ClientIpAddressLastFailedFetch;
    private string? ClientName { get; set; }
    private Guid? TenantId { get; set; }
    public List<string> TopicList { get; init; }
    public static ConcurrentDictionary<Type, string> TypeFriendlyNameCache = new();

    public bool IsConnected => SignalRService.Connection?.State == HubConnectionState.Connected;

    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }

    public BoltDriverSignalR(CacheManager cache, IHostEnvironment hostEnvironment, ISignalRService signalRService, IConfiguration configuration, ILogger<BoltDriverSignalR> logger, IHttpClientFactory httpClientFactory, DeviceAgentProvider deviceAgentProvider)
    {
        Cache = cache;
        HostEnvironment = hostEnvironment;
        HttpClientFactory = httpClientFactory;
        HttpClient = HttpClientFactory.CreateClient();
        DeviceAgentProvider = deviceAgentProvider;
        SignalRService = signalRService;
        Configuration = configuration;
        Logger = logger;

        if (SignalRService.Connection is not null)
        {
            SignalRService.Connection.Reconnected += (e) =>
            {
                Logger.LogInformation("Reconnected to SignalR.");
                OnReconnected?.Invoke();
                return Task.CompletedTask;
            };
            SignalRService.Connection.Reconnecting += (e) =>
            {
                Logger.LogWarning("Attempting to reconnect to SignalR...");
                OnReconnecting?.Invoke();
                return Task.CompletedTask;
            };
            SignalRService.Connection.Closed += async (e) =>
            {
                Logger.LogError("Connection to SignalR closed. Attempting to reconnect...");
                await AttemptReconnect();
                OnDisconnected?.Invoke();
            };
        }
    }

    private async Task AttemptReconnect()
    {
        const int maxRetries = 1_000_000;
        int retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                Logger.LogInformation("Reconnection attempt {RetryCount}...", retryCount + 1);
                await SignalRService.EnsureConnection();
                Logger.LogInformation("Successfully reconnected to SignalR.");
                return;
            }
            catch (Exception ex)
            {
                Logger.LogError("Reconnection attempt {RetryCount} failed: {ErrorMessage}", retryCount + 1, ex.Message);
                retryCount++;
                await Task.Delay(TimeSpan.FromSeconds(5)); // Exponential backoff
            }
        }
        Logger.LogError("Failed to reconnect to SignalR after {MaxRetries} attempts.", maxRetries);
    }
    
    private static string GetRequestFriendlyName(Type type)
    {
        return TypeFriendlyNameCache.GetOrAdd(type, static t =>
        {
            if (t.IsGenericParameter)
                return t.Name;

            if (!t.IsGenericType)
                return t.FullName ?? t.Name;

            var nameSpan = t.Name.AsSpan();
            var index = nameSpan.IndexOf('`');
            var prefix = index == -1 ? nameSpan : nameSpan.Slice(0, index);

            var builder = new StringBuilder();
            builder.Append(prefix).Append('<');
            var first = true;
            foreach (var arg in t.GetGenericArguments())
            {
                if (!first)
                    builder.Append(',');

                builder.Append(arg.FullName != null && arg.FullName.Contains('.') ? arg.Name : GetRequestFriendlyName(arg));
                first = false;
            }
            builder.Append('>');
            return builder.ToString();
        });
    }
    
    public async Task<bool> Connect()
    {
        _ = FetchClientIpAddressOnceAsync();
        return await SignalRService.EnsureConnection();
    }

    private async Task FetchClientIpAddressOnceAsync()
    {
        if (ClientIpAddress is not null) return;

        try
        {
            Logger.LogInformation("Attempting to get client IP address");

            var httpClient = HttpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMilliseconds(500);
            var ipAddress = await httpClient.GetStringAsync("https://api.ipify.org/");
            ClientIpAddress = ipAddress;

            Logger.LogInformation("Client IP address acquired: {ClientIpAddress}", ClientIpAddress);
        }
        catch (Exception e)
        {
            ClientIpAddressLastFailedFetch = DateTime.Now;
            Logger.LogError("Unable to get client IP address: {ErrorMessage}", e.Message);
            ClientIpAddress = string.Empty;
        }
    }

    private async Task<RequestMetadata> GetRequestServer<TRequest>(TRequest request)
        where TRequest : IHasRequestServer
    {
        // Ensure client IP address is fetched (lazy one-time)
        if (string.IsNullOrEmpty(ClientIpAddress))
        {
            await FetchClientIpAddressOnceAsync();
        }

        // ApplicationId validation and assignment
        TenantId ??= Guid.TryParse(Configuration.GetValue<string>("Tenant:DefaultId"), out var appId)
            ? appId
            : throw new ArgumentNullException(nameof(appId), "Tenant:DefaultId is not set in appsettings.json");

        // Bolt ClientId validation and assignment
        ClientName ??= Configuration.GetValue<string>("BoltConfiguration:ClientName")
                       ?? throw new ArgumentNullException(nameof(ClientName), "BoltConfiguration:ClientName is not set in appsettings.json");

        // Extracting properties from request
        var requestServer = request.Metadata;

        return new()
        {
            DeviceAgent = DeviceAgentProvider.Name,
            TenantId = requestServer?.TenantId ?? TenantId,
            Name = !string.IsNullOrEmpty(requestServer?.Name) ? requestServer.Name : ClientName,
            IpAddress = !string.IsNullOrEmpty(requestServer?.IpAddress) ? requestServer.IpAddress : ClientIpAddress,
            RequestId = requestServer?.RequestId ?? Guid.NewGuid(),
            SessionId = request.Metadata?.SessionId ?? Cache.Get<Guid>("ActiveSession:SessionId")
        };
    }
    
    private async Task SetRequestServer<TRequest>(TRequest request)
        where TRequest : class, IHasRequestServer
    {
        var rs = await GetRequestServer(request);
        request.Metadata = rs;
    }

    public Task StartClientEventListener(string topic)
    {
        return SignalRService.StartEventListener(topic);
    }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, string recipient) 
        where TRequest : class, IHasRequestServer
    {
        await SetRequestServer(request);
        var r = new BoltMessage<TRequest>(request)
        {
            ExchangeType = MessageExchangeType.Direct,
            RecipientId = recipient,
            CommandName = GetRequestFriendlyName(typeof(TRequest))
        };
        
        var result = await InvokeAsync<TRequest, CmdResponse>(r);
        r.Dispose();

#if DEBUG
        if (!HostEnvironment.IsProduction())
        {
            Task.Run(() =>
            {
                var serviceRequestLog = new ServiceRequestLog<TRequest, CmdResponse>(Request: request, Response: result.Response);
                Logger.LogWarning("Service Request Log: {$ServiceRequestLog}", serviceRequestLog);
            });
        }
#endif
        
        return result.Response;
    }

    public async Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        await SetRequestServer(request);
        var r = new BoltMessage<TRequest>(request)
        {
            ExchangeType = MessageExchangeType.Direct,
            RecipientId = recipient,
            CommandName = GetRequestFriendlyName(typeof(TRequest))
        };
        
        var result = await InvokeAsync<TRequest, CmdResponse<TResponse>>(r);
        r.Dispose();
#if DEBUG
        Task.Run(() =>
        {
            var serviceRequestLog = new ServiceRequestLog<TRequest, CmdResponse<TResponse>>(Request: request, Response: result.Response);
            Logger.LogWarning("Service Request Log: {$ServiceRequestLog}", serviceRequestLog);
        });
#endif

        if (result.HttpStatusCode is HttpStatusCode.InternalServerError)
        {
            throw new(result.Message);
        }
        return result.Response;
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, string recipient) 
        where TRequest : class, IHasRequestServer
    {
        await SetRequestServer(request);
        var r = new BoltMessage<TRequest>(request)
        {
            ExchangeType = MessageExchangeType.Direct,
            RecipientId = recipient,
            CommandName = GetRequestFriendlyName(typeof(TRequest))
        };
        
        var result = await InvokeAsync<TRequest, QueryResponse<TResponse>>(r);
        
#if DEBUG
        Task.Run(() =>
        {
            var serviceRequestLog = new ServiceRequestLog<TRequest, QueryResponse<TResponse>>(Request: request, Response: result.Response);
            Logger.LogWarning("Service Request Log: {$ServiceRequestLog}", serviceRequestLog);
        });
#endif

        return result.Response;
    }

    public async Task<BoltInvokeResult<TResponse>> InvokeAsync<TModel, TResponse>(BoltMessage<TModel> request)
        where TModel : class, IHasRequestServer
        where TResponse : class, IBaseResponse
    {
        Logger.LogDebug("Sending request {Request}...", request.CommandName);

        var rpcResult = await SignalRService.InvokeAsync(request);

        switch (rpcResult.StatusCode)
        {
            case HttpStatusCode.Processing:
            {
                request.Dispose();
                var err = Activator.CreateInstance<TResponse>();
                err.Message = "Request is queued, waiting for connection to be re-established";
                err.HttpStatusCode = HttpStatusCode.Processing;
                return new() { HttpStatusCode = err.HttpStatusCode, Response = err };
            }
            case HttpStatusCode.NotFound:
            {
                request.Dispose();
                var err = Activator.CreateInstance<TResponse>();
                err.Message = "Service is currently offline";
                err.HttpStatusCode = HttpStatusCode.NotFound;
                return new() { HttpStatusCode = err.HttpStatusCode, Response = err };
            }
            case HttpStatusCode.InternalServerError:
            {
                Logger.LogError("Sending request: {Request}... Failed in {ResponseTime}ms => {StatusCode}", request.CommandName, rpcResult.Duration.TotalMilliseconds, rpcResult.StatusCode);
                request.Dispose();

                return new()
                {
                    HttpStatusCode = rpcResult.StatusCode,
                    Message = rpcResult.Message,
                    Response = MemoryPackSerializer.Deserialize<TResponse>(rpcResult.Data.Span)
                };
            }
            default:
                var t = MemoryPackSerializer.Deserialize<TResponse>(rpcResult.Data.Span);
                Logger.LogDebug("Request {Request} completed in {ResponseTime}ms => {StatusCode}", request.CommandName, rpcResult.Duration.TotalMilliseconds, t.HttpStatusCode);
                request.Dispose();

                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    Response = t
                };
                break;
        }
        return new();
    }

    public async Task PublishAsync<TRequest>(string eventName, string topic, TRequest? request) 
        where TRequest : class, IHasRequestServer
    {
        await SetRequestServer(request);
        var r = new BoltMessage<TRequest>(request)
        {
            ExchangeType = MessageExchangeType.Topic,
            Topic = topic,
            CommandName = eventName,
        };
        
        Task.Run(() =>
        {
            var serviceRequestLog = new ServiceRequestLog<TRequest>(Request: request);
            Logger.LogInformation("Publishing broadcast request: {@ServiceRequestLog}", serviceRequestLog);
        });
        
        await PushAsync(r);
    }
    
    public async Task PublishAsync(string eventName, string topic)
    {
        var request = new RequestBase();
        await SetRequestServer(request);
        var r = new BoltMessage<RequestBase>(request)
        {
            ExchangeType = MessageExchangeType.Topic,
            Topic = topic,
            CommandName = eventName,
        };
        
        Task.Run(() =>
        {
            var serviceRequestLog = new ServiceRequestLog<RequestBase>(Request: request);
            Logger.LogInformation("Publishing broadcast request: {@ServiceRequestLog}", serviceRequestLog);
        });
        
        await PushAsync(r);
    }

    public async Task PushAsync<TModel>(BoltMessage<TModel> request) 
        where TModel : class, IHasRequestServer
    {
        //request.Recipient ??= TargetClient;
        await SignalRService.InvokeVoidAsync(nameof(IBoltTransport.Push), request as BoltMessage);
    }

    public Task Subscribe<TResponse>(BoltSubscriptionRequest<TResponse> request) 
        where TResponse : class
    {
        Logger.LogInformation("Subscribing to {RequestName}...", request.Name);
        SignalRService.Connection.On<BoltMessage>(request.Name,
            async (response) =>
            {
                Logger.LogInformation("Notification Received: {RequestName}", request.Name);
                try
                {
                    var r = MemoryPackSerializer.Deserialize<PublishRequest<TResponse>>(response.Data.Span);
                    
                    request.OnInvoke?.Invoke(r.Data);
                }
                catch (Exception e)
                {
                    Logger.LogInformation("Notification Received Exception: {EMessage} : {InnerExceptionMessage}", e.Message, e.InnerException?.Message);
                }
            });
        return Task.CompletedTask;
    }

    public Task Unsubscribe(BoltSubscriptionRequest request)
    {
        SignalRService.Connection.Remove(request.Name);
        return Task.CompletedTask;
    }
}

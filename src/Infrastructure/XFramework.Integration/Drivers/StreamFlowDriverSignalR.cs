using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MemoryPack;
using MessagePack;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Shared.Abstractions;
using StreamFlow.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Entity.Contracts.Responses;

namespace XFramework.Integration.Drivers;

public class StreamFlowDriverSignalR : IMessageBusWrapper
{
    public ISignalRService SignalRService { get; set; }
    private IConfiguration Configuration { get; set; }
    private HttpClient HttpClient { get; set; }
    public IHostEnvironment HostEnvironment { get; }
    private IHttpClientFactory HttpClientFactory { get; set; }
    private DeviceAgentProvider DeviceAgentProvider { get; set; }
    private ILogger<StreamFlowDriverSignalR> Logger { get; }
    private string? ClientIpAddress { get; set; }
    private DateTime ClientIpAddressLastFailedFetch { get; set; }
    private TimeSpan ClientIpAddressFetchTimeout => DateTime.Now - ClientIpAddressLastFailedFetch;
    private string? ClientName { get; set; }
    private Guid? TenantId { get; set; }
    public List<string> TopicList { get; init; }
    public static Dictionary<Type, string> TypeFriendlyNameCache = new();


    public HubConnectionState ConnectionState => SignalRService.Connection.State;

    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }

    public StreamFlowDriverSignalR(IHostEnvironment hostEnvironment, ISignalRService signalRService, IConfiguration configuration, ILogger<StreamFlowDriverSignalR> logger, IHttpClientFactory httpClientFactory, DeviceAgentProvider deviceAgentProvider)
    {
        HostEnvironment = hostEnvironment;
        HttpClientFactory = httpClientFactory;
        HttpClient = HttpClientFactory.CreateClient();
        DeviceAgentProvider = deviceAgentProvider;
        SignalRService = signalRService;
        Configuration = configuration;
        Logger = logger;

        SignalRService.Connection.Reconnected += async (e) => OnReconnected?.Invoke();
        SignalRService.Connection.Reconnecting += async (e) => OnReconnecting?.Invoke();
        SignalRService.Connection.Closed += async (e) => OnDisconnected?.Invoke();
    }
    
    private static string GetRequestFriendlyName(Type type)
    {
        if (TypeFriendlyNameCache.TryGetValue(type, out var cachedName))
        {
            return cachedName;
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var nameSpan = type.Name.AsSpan();
        var index = nameSpan.IndexOf('`');
        var prefix = index == -1 ? nameSpan : nameSpan.Slice(0, index);

        var builder = new StringBuilder();
        builder.Append(prefix).Append('<');
        var first = true;
        foreach (var arg in type.GetGenericArguments())
        {
            if (!first)
            {
                builder.Append(',');
            }

            var argName = arg.Name;  // Start with simple name

            // Check if arg's FullName contains namespaces, and if so, just use the simple name.
            if (arg.FullName != null && arg.FullName.Contains('.'))
            {
                builder.Append(argName);
            }
            else
            {
                builder.Append(GetRequestFriendlyName(arg));
            }

            first = false;
        }
        builder.Append('>');

        var friendlyName = builder.ToString();
        TypeFriendlyNameCache[type] = friendlyName;
        return friendlyName;
    }
    
    public async Task<bool> Connect()
    {
        Task.Run(async () =>
        {
            if (string.IsNullOrEmpty(ClientIpAddress))
            {
                
                var retryCount = 0;
                const int maxRetryCount = 5;
                
                try
                {
                    Logger.LogInformation("Attempting to get client IP address");
                    
                    var ipAddress = await new HttpClient()
                    {
                        Timeout = TimeSpan.FromMilliseconds(500)
                    }.GetStringAsync("https://api.ipify.org/");
                    ClientIpAddress = ipAddress;
                    
                    Logger.LogInformation("Client IP address acquired: {ClientIpAddress}", ClientIpAddress);

                }
                catch (Exception e)
                {
                    ClientIpAddressLastFailedFetch = DateTime.Now;
                    Logger.LogError("Unable to get client IP address");
                    ClientIpAddress = string.Empty;
                }
            }
        });
        return await SignalRService.EnsureConnection();
    }

    private async Task<RequestMetadata> GetRequestServer<TRequest>(TRequest request)
        where TRequest : IHasRequestServer
    {
        // Retrieving Client IP address
        if (string.IsNullOrEmpty(ClientIpAddress))
        {
            try
            {
                if (ClientIpAddressFetchTimeout < TimeSpan.FromMinutes(2))
                {
                    Logger.LogError("Unable to get client IP address");
                    ClientIpAddress = string.Empty; 
                }
                Logger.LogInformation("Attempting to get client IP address");

                var ipAddress = await new HttpClient()
                {
                    Timeout = TimeSpan.FromMilliseconds(500)
                }.GetStringAsync("https://api.ipify.org/");
                ClientIpAddress = ipAddress;

                Logger.LogInformation("Client IP address acquired: {ClientIpAddress}", ClientIpAddress);
            }
            catch (Exception e)
            {
                Logger.LogError("Unable to get client IP address");
                ClientIpAddressLastFailedFetch = DateTime.Now;
                ClientIpAddress = string.Empty;
            }
        }

        // ApplicationId validation and assignment
        TenantId ??= Guid.TryParse(Configuration.GetValue<string>("Tenant:DefaultId"), out var appId)
            ? appId
            : throw new ArgumentNullException(nameof(appId), "Tenant:DefaultId is not set in appsettings.json");

        // StreamFlow ClientId validation and assignment
        ClientName ??= Configuration.GetValue<string>("StreamFlowConfiguration:ClientName")
                       ?? throw new ArgumentNullException(nameof(ClientName), "StreamFlowConfiguration:ClientName is not set in appsettings.json");

        // Extracting properties from request
        var requestServer = request.Metadata;

        return new()
        {
            DeviceAgent = DeviceAgentProvider.Name,
            TenantId = requestServer?.TenantId ?? TenantId,
            Name = !string.IsNullOrEmpty(requestServer?.Name) ? requestServer.Name : ClientName,
            IpAddress = !string.IsNullOrEmpty(requestServer?.IpAddress) ? requestServer.IpAddress : ClientIpAddress,
            RequestId = requestServer?.RequestId ?? Guid.NewGuid()
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
        var r = new StreamFlowMessage<TRequest>(request)
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

    public async Task<CmdResponse<TRequest>> SendAsync<TRequest>(TRequest request, string recipient)
        where TRequest : class, IHasRequestServer
    {
        await SetRequestServer(request);
        var r = new StreamFlowMessage<TRequest>(request)
        {
            ExchangeType = MessageExchangeType.Direct,
            RecipientId = recipient,
            CommandName = GetRequestFriendlyName(typeof(TRequest))
        };
        
        var result = await InvokeAsync<TRequest, CmdResponse<TRequest>>(r);
        r.Dispose();
#if DEBUG
        Task.Run(() =>
        {
            var serviceRequestLog = new ServiceRequestLog<TRequest, CmdResponse<TRequest>>(Request: request, Response: result.Response);
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
        var r = new StreamFlowMessage<TRequest>(request)
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

    public async Task<StreamFlowInvokeResult<TResponse>> InvokeAsync<TModel, TResponse>(StreamFlowMessage<TModel> request) 
        where TModel : class, IHasRequestServer
        where TResponse : class, IBaseResponse
    {
        Logger.LogInformation("Sending request {Request}...", request.CommandName);
        
        var signalRResponse = await SignalRService.InvokeAsync(request);
        var tResponse = Activator.CreateInstance<TResponse>();

        switch (signalRResponse.ResponseStatusCode)
        {
            case HttpStatusCode.Processing:
                tResponse.Message = "Request is queued, waiting for connection to be re-established";
                tResponse.HttpStatusCode = HttpStatusCode.Processing;

                request.Dispose();
                return new()
                {
                    HttpStatusCode = tResponse.HttpStatusCode,
                    Response = tResponse,
                };
            case HttpStatusCode.NotFound:
            {
                tResponse.Message = "Service is currently offline";
                tResponse.HttpStatusCode = HttpStatusCode.NotFound;
               
                request.Dispose();
                return new()
                {
                    HttpStatusCode = tResponse.HttpStatusCode,
                    Response = tResponse
                };
            }
            case HttpStatusCode.InternalServerError:
            {
                Logger.LogError("Sending request: {Request}... Failed in {Duration}ms => {StatusCode}", request.CommandName, signalRResponse.Duration.TotalMilliseconds, signalRResponse.ResponseStatusCode);
                request.Dispose();
                
                return new()
                {
                    HttpStatusCode = signalRResponse.ResponseStatusCode,
                    Message = signalRResponse.Message,
                    Response = await MemoryPackSerializer.DeserializeAsync<TResponse>(new MemoryStream(signalRResponse.Data))
                };
            }
            default:
                var sw = new Stopwatch();
                sw.Start();
                var t = await MemoryPackSerializer.DeserializeAsync<TResponse>(new MemoryStream(signalRResponse.Data));
                sw.Stop();
                Logger.LogWarning("Deserialization of response: {Request}... Done in {Duration}ms", request.CommandName, sw.ElapsedMilliseconds);
                Logger.LogInformation("Sending request: {Request}... Done in {Duration}ms => {StatusCode}", request.CommandName, signalRResponse.Duration.TotalMilliseconds, t.HttpStatusCode);
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
        var r = new StreamFlowMessage<TRequest>(request)
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
        var r = new StreamFlowMessage<RequestBase>(request)
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

    public async Task PushAsync<TModel>(StreamFlowMessage<TModel> request) 
        where TModel : class, IHasRequestServer
    {
        //request.Recipient ??= TargetClient;
        await SignalRService.InvokeVoidAsync(nameof(IStreamFlow.Push), request as StreamFlowMessage);
    }

    public Task Subscribe<TResponse>(StreamFlowSubscriptionRequest<TResponse> request) 
        where TResponse : class
    {
        Logger.LogInformation("Subscribing to {RequestName}...", request.Name);
        SignalRService.Connection.On<StreamFlowMessage>(request.Name,
            async (response) =>
            {
                Logger.LogInformation("Notification Received: {RequestName}", request.Name);
                try
                {
                    var r = await MemoryPackSerializer.DeserializeAsync<PublishRequest<TResponse>>(new MemoryStream(response.Data));
                    
                    request.OnInvoke?.Invoke(r.Data);
                }
                catch (Exception e)
                {
                    Logger.LogInformation("Notification Received Exception: {EMessage} : {InnerExceptionMessage}", e.Message, e.InnerException?.Message);
                }
            });
        return Task.CompletedTask;
    }

    public Task Unsubscribe(StreamFlowSubscriptionRequest request)
    {
        SignalRService.Connection.Remove(request.Name);
        return Task.CompletedTask;
    }
}
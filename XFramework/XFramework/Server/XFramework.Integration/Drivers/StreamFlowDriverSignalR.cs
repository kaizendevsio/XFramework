using System.Net.Http.Json;
using MessagePack;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Serilog;
using StreamFlow.Domain.Generic.Abstractions;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Entity.Contracts.Responses;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Drivers;

public class StreamFlowDriverSignalR : IMessageBusWrapper
{
    public ISignalRService SignalRService { get; set; }
    private IConfiguration Configuration { get; set; }
    private HttpClient HttpClient { get; set; }
    private IHttpClientFactory HttpClientFactory { get; set; }
    private DeviceAgentProvider DeviceAgentProvider { get; set; }
    private ILogger<StreamFlowDriverSignalR> Logger { get; }
    private string? ClientIpAddress { get; set; }
    private string? ClientName { get; set; }
    private Guid? TenantId { get; set; }
    public List<string> TopicList { get; init; }

    public HubConnectionState ConnectionState => SignalRService.Connection.State;

    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }

    public StreamFlowDriverSignalR(ISignalRService signalRService, IConfiguration configuration, ILogger<StreamFlowDriverSignalR> logger, IHttpClientFactory httpClientFactory, DeviceAgentProvider deviceAgentProvider)
    {
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
    
    public async Task<bool> Connect()
    {
        Task.Run(async () =>
        {
            if (string.IsNullOrEmpty(ClientIpAddress))
            {
                
                var retryCount = 0;
                const int maxRetryCount = 5;
                
                TryGetClientIpAddress:
                try
                {
                    Logger.LogInformation("Attempting to get client IP address");
                    
                    var jsonIpResponse = await new HttpClient().GetFromJsonAsync<JsonIpResponse>("https://jsonip.com/");
                    ClientIpAddress = jsonIpResponse?.IpAddress;
                    
                    Logger.LogInformation("Client IP address acquired: {ClientIpAddress}", ClientIpAddress);

                }
                catch (Exception e)
                {
                    if (retryCount < maxRetryCount)
                    {
                        Logger.LogInformation(e, "Unable to get client IP address, retrying...");
                        retryCount++;
                        goto TryGetClientIpAddress;
                    }
                    Logger.LogError(e, "Unable to get client IP address");
                    ClientIpAddress = string.Empty;
                }
            }
        });
        return await SignalRService.EnsureConnection();
    }

    private async Task<RequestServer> GetRequestServer<TRequest>(TRequest request)
        where TRequest : IHasRequestServer
    {
        // Retrieving Client IP address
        if (string.IsNullOrEmpty(ClientIpAddress))
        {
            try
            {
                Logger.LogInformation("Attempting to get client IP address");

                var jsonIpResponse = await HttpClient.GetFromJsonAsync<JsonIpResponse>("https://jsonip.com/");
                ClientIpAddress = jsonIpResponse?.IpAddress;

                Logger.LogInformation("Client IP address acquired: {ClientIpAddress}", ClientIpAddress);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Unable to get client IP address");
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

        return new RequestServer
        {
            DeviceAgent = DeviceAgentProvider.Name,
            TenantId = requestServer?.TenantId ?? TenantId,
            Name = !string.IsNullOrEmpty(requestServer?.Name) ? requestServer.Name : ClientName,
            IpAddress = !string.IsNullOrEmpty(requestServer?.IpAddress) ? requestServer.IpAddress : ClientIpAddress,
            RequestId = requestServer?.RequestId ?? Guid.NewGuid()
        };
    }
    
    private async Task SetRequestServer<TRequest>(TRequest request)
        where TRequest : IHasRequestServer, new()
    {
        var rs = await GetRequestServer(request);
        request.Metadata = rs;
    }

    public Task StartClientEventListener(string topic)
    {
        return SignalRService.StartEventListener(topic);
    }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, Guid? recipient) 
        where TRequest : IHasRequestServer, new()
    {
        await SetRequestServer(request);
        var r = new StreamFlowMessage<TRequest>(request)
        {
            ExchangeType = MessageExchangeType.Direct,
            RecipientId = recipient
        };

        Logger.LogInformation("Sending request: {@Request}", r);
        
        var result = await InvokeAsync<TRequest, CmdResponse>(r);
        
        Task.Run(() =>
        {
            var serviceRequestLog = new ServiceRequestLog<TRequest, CmdResponse>(Request: request, Response: result.Response);
            Logger.LogInformation("Service Request Log: {@ServiceRequestLog}", serviceRequestLog);
        });
        
        return result.Response;
    }

    public async Task<CmdResponse<TRequest>> SendAsync<TRequest>(TRequest request, Guid? recipient)
        where TRequest : IHasRequestServer, new()
    {
        await SetRequestServer(request);
        var r = new StreamFlowMessage<TRequest>(request)
        {
            ExchangeType = MessageExchangeType.Direct,
            RecipientId = recipient
        };

        Logger.LogInformation("Sending request: {@Request}", r);

        var result = await InvokeAsync<TRequest, CmdResponse<TRequest>>(r);
        
        Task.Run(() =>
        {
            var serviceRequestLog = new ServiceRequestLog<TRequest, CmdResponse<TRequest>>(Request: request, Response: result.Response);
            Logger.LogInformation("Service Request Log: {@ServiceRequestLog}", serviceRequestLog);
        });
        
        return result.Response;
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, Guid? recipient) 
        where TRequest : IHasRequestServer, new()
    {
        await SetRequestServer(request);
        var r = new StreamFlowMessage<TRequest>(request)
        {
            ExchangeType = MessageExchangeType.Direct,
            RecipientId = recipient
        };
        
        Logger.LogInformation("Sending request: {@Request}", r);

        var result = await InvokeAsync<TRequest, QueryResponse<TResponse>>(r);
        
        Task.Run(() =>
        {
            var serviceRequestLog = new ServiceRequestLog<TRequest, QueryResponse<TResponse>>(Request: request, Response: result.Response);
            Logger.LogInformation("Service Request Log: {@ServiceRequestLog}", serviceRequestLog);
        });
        
        return result.Response.Adapt<QueryResponse<TResponse>>();
    }

    public async Task<StreamFlowInvokeResult<TResponse>> InvokeAsync<TModel, TResponse>(StreamFlowMessage<TModel> request) 
        where TModel : new()
        where TResponse : IBaseResponse, new()
    {
        var signalRResponse = await SignalRService.InvokeAsync(request);
        var tResponse = Activator.CreateInstance<TResponse>();
        
        switch (signalRResponse.ResponseStatusCode)
        {
            case HttpStatusCode.Processing:
                tResponse.Message = "Request is queued, waiting for connection to be re-established";
                tResponse.HttpStatusCode = HttpStatusCode.Processing;

                return new()
                {
                    HttpStatusCode = tResponse.HttpStatusCode,
                    Response = tResponse
                };
            case HttpStatusCode.NotFound:
            {
                tResponse.Message = "Service is currently offline";
                tResponse.HttpStatusCode = HttpStatusCode.NotFound;

                return new()
                {
                    HttpStatusCode = tResponse.HttpStatusCode,
                    Response = tResponse
                };
            }
            default:
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    Response = MessagePackSerializer.Deserialize<TResponse>(signalRResponse.Data)
                };
                break;
        }
        return new();
    }

    public async Task PublishAsync<TRequest>(string eventName, string topic, TRequest request) 
        where TRequest : IHasRequestServer, new()
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

    public async Task PushAsync<TModel>(StreamFlowMessage<TModel> request) where TModel : new()
    {
        //request.Recipient ??= TargetClient;
        await SignalRService.InvokeVoidAsync(nameof(IStreamFlow.Push), request);
    }

    public Task Subscribe<TResponse>(StreamFlowSubscriptionRequest<TResponse> request) where TResponse : new()
    {
        SignalRService.Connection.On<StreamFlowMessage<TResponse>>(request.Name,
            async (response) =>
            {
                Logger.LogInformation("Notification Received: {RequestName}", request.Name);
                try
                {
                    var r = MessagePackSerializer.Deserialize<TResponse>(response.Data);
                    request.OnInvoke?.Invoke(r);
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
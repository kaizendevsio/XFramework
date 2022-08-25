using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using TypeSupport.Extensions;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Entity.Contracts.Responses;
using XFramework.Integration.Interfaces;

namespace XFramework.Integration.Drivers;

public class StreamFlowDriverSignalR : IMessageBusWrapper
{
    public ISignalRService SignalRService { get; set; }
    private IConfiguration Configuration { get; set; }
    private string ClientIpAddress { get; set; }
    private string ClientName { get; set; }
    private Guid? ApplicationId { get; set; }

    public HubConnectionState ConnectionState => SignalRService.Connection.State;

    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }

    public StreamFlowDriverSignalR(ISignalRService signalRService, IConfiguration configuration)
    {
        SignalRService = signalRService;
        Configuration = configuration;

        SignalRService.Connection.Reconnected += async (e) => OnReconnected?.Invoke();
        SignalRService.Connection.Reconnecting += async (e) => OnReconnecting?.Invoke();
        SignalRService.Connection.Closed += async (e) => OnDisconnected?.Invoke();
    }
    
    public async Task<bool> Connect()
    {
        return await SignalRService.EnsureConnection();
    }

    private async Task<RequestServerBO> GetRequestServer<TRequest>(TRequest request)
    {
        if (string.IsNullOrEmpty(ClientIpAddress))
        {
            try
            {
                var jsonIpResponse = await new HttpClient().GetFromJsonAsync<JsonIpResponse>("https://jsonip.com/");
                ClientIpAddress = jsonIpResponse?.IpAddress;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        ApplicationId ??= Guid.Parse(Configuration.GetValue<string>("Application:DefaultUID"));
        ClientName = !string.IsNullOrEmpty(ClientName)
            ? ClientName
            : Configuration.GetValue<string>("StreamFlowConfiguration:ClientName");

        var applicationId = ApplicationId;
        var requestId = Guid.NewGuid();
        var ipAddress = string.Empty;
        var deviceAgent = string.Empty;
        var clientName = string.Empty;

        if (request.ContainsProperty("RequestServer"))
        {
            var requestServer = request.GetPropertyValue("RequestServer").Adapt<RequestServerBO>();

            applicationId = requestServer?.ApplicationId ?? applicationId;
            requestId = requestServer?.RequestId ?? requestId;

            clientName = !string.IsNullOrEmpty(requestServer?.Name)
                ? requestServer.Name
                : ClientName;
            ipAddress = !string.IsNullOrEmpty(requestServer?.IpAddress)
                ? requestServer.IpAddress
                : ClientIpAddress;
        }

        return new()
        {
            DeviceAgent = deviceAgent,
            ApplicationId = applicationId,
            Name = clientName,
            IpAddress = ipAddress,
            RequestId = requestId
        };
    }
    
    private async Task SetRequestServer<TRequest>(TRequest request) where TRequest : new()
    {
        try
        {
            if (request.ContainsProperty("RequestServer"))
            {
                var rs = await GetRequestServer(request);
                request.SetPropertyValue("RequestServer", rs);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public Task StartClientEventListener(Guid? credentialGuid)
    {
        return SignalRService.StartEventListener(credentialGuid);
    }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, Guid? recipient) where TRequest : new()
    {
        await SetRequestServer(request);
        var r = new StreamFlowMessageBO
        {
            ExchangeType = MessageExchangeType.Direct,
            Recipient = recipient
        };
        r.SetData(request);

        var result = await InvokeAsync<CmdResponse>(r);
        return result.Response;
    }

    public async Task<CmdResponse<TRequest>> SendAsync<TRequest>(TRequest request, Guid? recipient) where TRequest : new()
    {
        await SetRequestServer(request);
        var r = new StreamFlowMessageBO
        {
            ExchangeType = MessageExchangeType.Direct,
            Recipient = recipient
        };
        r.SetData(request);

        var result = await InvokeAsync<CmdResponse<TRequest>>(r);
        return result.Response;
    }

    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, Guid? recipient) where TRequest : new()
    {
        await SetRequestServer(request);
        var r = new StreamFlowMessageBO
        {
            ExchangeType = MessageExchangeType.Direct,
            Recipient = recipient
        };
        r.SetData(request);

        var result = await InvokeAsync<QueryResponse<TResponse>>(r);
        /*Task.Run(async () =>
        {
            if (Logger is not null)
            {
                var serviceRequestLog = new ServiceRequestLog<TRequest, QueryResponse<TResponse>>(){Request = request, Response = result.Response};
                await Logger.Log("Service Request", JsonSerializer.Serialize(serviceRequestLog), this, rs.RequestId , LogLevel.Trace, LogType.SystemMaintenance);
            }
        });*/
        return result.Response.Adapt<QueryResponse<TResponse>>();
    }

    public async Task<StreamFlowInvokeResult<TResponse>> InvokeAsync<TResponse>(StreamFlowMessageBO request)
    {
        var signalRResponse = await SignalRService.InvokeAsync(request);
        var tResponse = Activator.CreateInstance<TResponse>();

        switch (signalRResponse.HttpStatusCode)
        {
            case HttpStatusCode.Processing:
                tResponse.SetPropertyValue("Message", "Request is queued, waiting for connection to be re-established");
                tResponse.SetPropertyValue("HttpStatusCode", (int) HttpStatusCode.Processing);

                return new()
                {
                    HttpStatusCode = HttpStatusCode.Processing,
                    Response = tResponse
                };
            case HttpStatusCode.NotFound:
            {
                tResponse.SetPropertyValue("Message", "Service is currently offline");
                tResponse.SetPropertyValue("HttpStatusCode", (int) HttpStatusCode.NotFound);

                return new()
                {
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Response = tResponse
                };
            }
            default:
                var options = new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles};
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    Response = JsonSerializer.Deserialize<TResponse>(signalRResponse.Response, options)
                };
                break;
        }

        return new();
    }

    public async Task PublishAsync<TData>(string eventName, Guid? recipient, TData data)
    {
        var options = new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles};
        var r = new StreamFlowMessageBO
        {
            ExchangeType = MessageExchangeType.Topic,
            Recipient = recipient,
            CommandName = eventName,
            Data = JsonSerializer.Serialize(data, options)
        };
        await SetRequestServer(r);
        await PushAsync(r);
    }

    public async Task PushAsync(StreamFlowMessageBO request)
    {
        //request.Recipient ??= TargetClient;
        await SignalRService.InvokeVoidAsync("Push", request);
    }

    public Task Subscribe<TResponse>(StreamFlowSubscriptionRequest<TResponse> request)
    {
        SignalRService.Connection.On<StreamFlowContract>(request.Name,
            async (response) =>
            {
                Console.WriteLine($"Notification Received: {request.Name}");
                try
                {
                    var r = JsonSerializer.Deserialize<TResponse>(response.Data);
                    request.OnInvoke?.Invoke(r);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Notification Received Exception: {e.Message} : {e.InnerException?.Message}");
                }
            });
        return Task.CompletedTask;
    }

    public Task Unsubscribe(StreamFlowSubscriptionRequest request)
    {
        throw new NotImplementedException();
    }
}
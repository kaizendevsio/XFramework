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
    public Guid? TargetClient { get; set; }
    public HubConnectionState ConnectionState => SignalRService.Connection.State;

    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }
    public StreamFlowDriverSignalR(ISignalRService signalRService)
    {
        SignalRService = signalRService;

        SignalRService.Connection.Reconnected += async (e) => OnReconnected?.Invoke();
        SignalRService.Connection.Reconnecting += async (e) => OnReconnecting?.Invoke();
        SignalRService.Connection.Closed += async (e) => OnDisconnected?.Invoke();
    }

    public async Task<bool> Connect()
    {
        return await SignalRService.EnsureConnection();
    }

    public async Task<StreamFlowInvokeResult<TResponse>> InvokeAsync<TResponse>(StreamFlowMessageBO request)
    {
        var signalRResponse = await SignalRService.InvokeAsync(request);
        var tResponse = Activator.CreateInstance<TResponse>();
            
        switch (signalRResponse.HttpStatusCode)
        {
            case HttpStatusCode.Processing:
                tResponse.SetPropertyValue("Message", "Request is queued, waiting for connection to be re-established");
                tResponse.SetPropertyValue("HttpStatusCode", (int)HttpStatusCode.Processing);
                
                return new(){
                    HttpStatusCode = HttpStatusCode.Processing,
                    Response = tResponse
                };
            case HttpStatusCode.NotFound:
            {
                tResponse.SetPropertyValue("Message", "Service is currently offline");
                tResponse.SetPropertyValue("HttpStatusCode", (int)HttpStatusCode.NotFound);

                return new(){
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Response = tResponse
                };
            }
            default:
                var options = new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles};
                return new(){
                    HttpStatusCode = HttpStatusCode.Accepted,
                    Response = JsonSerializer.Deserialize<TResponse>(signalRResponse.Response, options)
                };
                break;
            }
        return new();
    }

    public async Task PushAsync(StreamFlowMessageBO request)
    {
        //request.Recipient ??= TargetClient;
        await SignalRService.InvokeVoidAsync("Push", request);
    }

    /*public async Task Subscribe(StreamFlowClientBO request)
    {
        await SignalRService.InvokeVoidAsync("Subscribe", request);
    }
        
    public async Task Unsubscribe(StreamFlowClientBO request)
    {
        await SignalRService.InvokeVoidAsync("Unsubscribe", BinaryConverter.Serialize(request));
    }*/

}
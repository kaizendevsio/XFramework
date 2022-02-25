using System.Text.Json;
using MessagePack;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Integration.Entity.Contracts.Responses;
using XFramework.Integration.Interfaces;

namespace XFramework.Integration.Drivers
{
    public class StreamFlowDriverSignalR : IMessageBusWrapper
    {
        private ISignalRService SignalRService { get; set; }
        public Guid? TargetClient { get; set; }
        public HubConnectionState ConnectionState => SignalRService.Connection.State;

        public StreamFlowDriverSignalR(ISignalRService signalRService)
        {
            SignalRService = signalRService;
        }

        public async Task<bool> Connect()
        {
             return await SignalRService.EnsureConnection();
        }

        public async Task<StreamFlowInvokeResult<TResponse>> InvokeAsync<TResponse>(StreamFlowMessageBO request)
        {
            //request.Recipient ??= TargetClient;
            var signalRResponse = await SignalRService.InvokeAsync(request);
            var tResponse = Activator.CreateInstance<TResponse>();
            
            switch (signalRResponse.HttpStatusCode)
            {
                case HttpStatusCode.Processing:
                    
                    tResponse.GetType().GetProperty("Message")?
                        .SetValue(tResponse, $"Request is queued, waiting for connection to be re-established", null);
                    tResponse.GetType().GetProperty("HttpStatusCode")?
                        .SetValue(tResponse, HttpStatusCode.Processing, null);
                
                    return new(){
                        HttpStatusCode = HttpStatusCode.Processing,
                        Response = tResponse
                    };
                case HttpStatusCode.NotFound:
                {
                    tResponse.GetType().GetProperty("Message")?
                        .SetValue(tResponse, $"Service is currently offline", null);
                    tResponse.GetType().GetProperty("HttpStatusCode")?
                        .SetValue(tResponse, HttpStatusCode.NotFound, null);
                
                    return new(){
                        HttpStatusCode = HttpStatusCode.NotFound,
                        Response = tResponse
                    };
                }
                default:
                    return new(){
                        HttpStatusCode = HttpStatusCode.Accepted,
                        Response = JsonSerializer.Deserialize<TResponse>(signalRResponse.Response)
                    };
            }
        }

        public async Task PushAsync(StreamFlowMessageBO request)
        {
            request.Recipient ??= TargetClient;
            await SignalRService.InvokeVoidAsync("Push", request);
        }

        public async Task Subscribe(StreamFlowClientBO request)
        {
            await SignalRService.InvokeVoidAsync("Subscribe", request);
        }
        
        public async Task Unsubscribe(StreamFlowClientBO request)
        {
            await SignalRService.InvokeVoidAsync("Unsubscribe", request);
        }

    }
}
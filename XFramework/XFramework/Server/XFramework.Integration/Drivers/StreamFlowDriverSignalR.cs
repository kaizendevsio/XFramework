using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Mapster;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Entity.Contracts.Responses;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Integration.Drivers
{
    public class StreamFlowDriverSignalR : IMessageBusWrapper
    {
        private ISignalRService SignalRService { get; set; }
        public Guid? TargetClient { get; set; }
        

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
            request.Recipient ??= TargetClient;
            var signalRResponse = await SignalRService.InvokeAsync(request);
            
            return new(){
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = JsonSerializer.Deserialize<TResponse>(signalRResponse.Response)
            };
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
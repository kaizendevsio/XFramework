using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Mapster;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
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
            var signalRResponse = await SignalRService.InvokeAsync("Invoke", request);

            if (signalRResponse.HttpStatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
            {
                return new StreamFlowInvokeResult<TResponse>()
                {
                    Message = signalRResponse.Message,
                    HttpStatusCode = signalRResponse.HttpStatusCode
                };
            }
            return new StreamFlowInvokeResult<TResponse>()
            {
                Response = JsonSerializer.Deserialize<TResponse>(signalRResponse.Response),
                Message = signalRResponse.Message,
                HttpStatusCode = signalRResponse.HttpStatusCode
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
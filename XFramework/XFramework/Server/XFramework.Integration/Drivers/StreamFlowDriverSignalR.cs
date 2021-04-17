using System;
using System.Threading.Tasks;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;
using XFramework.Integration.Services;

namespace XFramework.Integration.Drivers
{
    public class StreamFlowDriverSignalR : IMessageBusWrapper
    {
        private SignalRService SignalRService { get; set; }
        public Guid? TargetClient { get; set; }

        public StreamFlowDriverSignalR(SignalRService signalRService)
        {
            SignalRService = signalRService;
        }

        public async Task<bool> Connect()
        {
             return await SignalRService.EnsureConnection();
        }

        public async Task Push(StreamFlowMessageBO request)
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
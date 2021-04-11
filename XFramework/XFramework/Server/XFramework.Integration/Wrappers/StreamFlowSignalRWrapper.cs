using System.Threading.Tasks;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;

namespace XFramework.Integration.Wrappers
{
    public class StreamFlowSignalRWrapper : IStreamFlowWrapper
    {
        private SignalRService SignalRService { get; set; }

        public StreamFlowSignalRWrapper(SignalRService signalRService)
        {
            SignalRService = signalRService;
        }

        public async Task<bool> Connect()
        {
             return await SignalRService.EnsureConnection();
        }

        public async Task Push(StreamFlowMessageBO request)
        {
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
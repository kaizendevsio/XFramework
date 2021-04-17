using System;
using System.Threading.Tasks;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Integration.Interfaces.Wrappers
{
    public interface IMessageBusWrapper : IXFrameworkService
    {
        public Guid? TargetClient { get; set; }
        public Task<bool> Connect();
        public Task Push(StreamFlowMessageBO request);
        public Task Subscribe(StreamFlowClientBO request);
        public Task Unsubscribe(StreamFlowClientBO request);
    }
}
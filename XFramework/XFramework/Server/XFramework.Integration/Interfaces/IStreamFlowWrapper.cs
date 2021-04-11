using System.Threading.Tasks;
using StreamFlow.Domain.BusinessObjects;

namespace XFramework.Integration.Interfaces
{
    public interface IStreamFlowWrapper
    {
        public Task<bool> Connect();
        public Task Push(StreamFlowMessageBO request);
        public Task Subscribe(StreamFlowClientBO request);
        public Task Unsubscribe(StreamFlowClientBO request);
    }
}
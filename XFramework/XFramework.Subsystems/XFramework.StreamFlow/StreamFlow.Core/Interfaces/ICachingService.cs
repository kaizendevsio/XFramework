using XFramework.Domain.Generic.Contracts.Base;

namespace StreamFlow.Core.Interfaces;

public interface ICachingService
{
    public ConcurrentDictionary<int,StreamFlowClient> LatestClients { get; set; }
    public ConcurrentDictionary<int,StreamFlowClient> Clients { get; set; }
    public ConcurrentDictionary<int,StreamFlowClient> AbsoluteClients { get; set; }
    public ConcurrentDictionary<Guid,StreamFlowMessage > QueuedMessages { get; set; }
    public ConcurrentDictionary<Guid, TaskCompletionSource<StreamFlowMessage >> PendingMethodCalls { get; set; }
        
}
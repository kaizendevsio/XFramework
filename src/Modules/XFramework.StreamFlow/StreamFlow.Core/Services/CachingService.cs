using XFramework.Domain.Shared.Contracts.Base;

namespace StreamFlow.Core.Services;

public class CachingService : ICachingService
{
    public CachingService()
    {
    }

    public ConcurrentDictionary<int, StreamFlowClient> Clients { get; set; } = new();
    public ConcurrentDictionary<int, StreamFlowClient> LatestClients { get; set; } = new();
    public ConcurrentDictionary<int, StreamFlowClient> AbsoluteClients { get; set; } = new();
    public ConcurrentDictionary<Guid, StreamFlowMessage > QueuedMessages { get; set; } = new();
    public ConcurrentDictionary<Guid, TaskCompletionSource<StreamFlowMessage >> PendingMethodCalls { get; set; } = new();
}